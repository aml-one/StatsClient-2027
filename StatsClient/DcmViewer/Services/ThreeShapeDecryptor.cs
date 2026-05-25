using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DCMViewer.Services
{
    /// <summary>
    /// Wrapper for 3Shape encryption assemblies. It discovers decryptor DLLs,
    /// finds supported Decrypt signatures via reflection, and tries them in rank order.
    /// </summary>
    public static class ThreeShapeDecryptor
    {
        private static readonly List<DecryptMethodBinding> _bindings = new();
        private static readonly HashSet<string> _probingDirectories = new(StringComparer.OrdinalIgnoreCase);
        private static bool _assemblyResolverAttached;
        private static bool _initialized;
        private static bool _available;
        private static string? _preferredInstallDirectory;

        private sealed record DecryptMethodBinding(
            string AssemblyPath,
            Type Type,
            MethodInfo Method,
            object? Instance,
            int Score);

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                SetupSecurityBypass();
                AttachAssemblyResolverOnce();

                var candidateDlls = DiscoverCandidateDlls().ToList();
                if (candidateDlls.Count == 0)
                {
                    Console.Error.WriteLine("[ThreeShapeDecryptor] No 3Shape decryptor DLL candidates were found.");
                    return;
                }

                Console.Error.WriteLine($"[ThreeShapeDecryptor] Found {candidateDlls.Count} candidate DLL(s):");
                foreach (var dllPath in candidateDlls)
                {
                    Console.Error.WriteLine($"[ThreeShapeDecryptor]   Scanning: {dllPath}");
                    TryRegisterAssembly(dllPath);
                }

                if (_bindings.Count > 0)
                {
                    _bindings.Sort((left, right) => right.Score.CompareTo(left.Score));
                    _available = true;
                    var distinctAssemblies = _bindings
                        .Select(binding => binding.AssemblyPath)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    Console.Error.WriteLine($"[ThreeShapeDecryptor] Initialized with {_bindings.Count} decrypt method(s) from {distinctAssemblies.Count} assembly(ies).\n  - {string.Join("\n  - ", distinctAssemblies)}");
                }
                else
                {
                    Console.Error.WriteLine("[ThreeShapeDecryptor] Could not find supported Decrypt methods in discovered DLLs.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ThreeShapeDecryptor] Failed to initialize: {ex.Message}");
            }
        }

        public static bool IsAvailable()
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _available;
        }

        public static void ConfigurePreferredInstall(IReadOnlyDictionary<string, string>? metadata)
        {
            if (_initialized || metadata is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("THREESHAPE_DECRYPTOR_DLL")))
            {
                return;
            }

            var preferred = ResolvePreferredInstallDirectory(metadata);
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                _preferredInstallDirectory = preferred;
                Console.Error.WriteLine($"[ThreeShapeDecryptor] Preferred install directory: {_preferredInstallDirectory}");
            }
        }

        public static byte[] TryDecryptFile(string filePath, IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (!IsAvailable())
            {
                return Array.Empty<byte>();
            }

            try
            {
                Console.Error.WriteLine($"[ThreeShapeDecryptor] Attempting to decrypt {Path.GetFileName(filePath)}");
                var originalBytes = File.ReadAllBytes(filePath);

                foreach (var binding in _bindings)
                {
                    var method = binding.Method;
                    var target = method.IsStatic ? null : binding.Instance;
                    if (!method.IsStatic && target is null)
                    {
                        continue;
                    }

                    var parameterCount = method.GetParameters().Length;
                    byte[] decrypted = Array.Empty<byte>();

                    try
                    {
                        if (parameterCount == 1)
                        {
                            decrypted = TryInvokeSingleArgumentDecrypt(target, method, filePath);
                        }
                        else if (parameterCount == 2)
                        {
                            decrypted = TryInvokeTwoArgumentDecrypt(target, method, filePath, metadata, originalBytes);
                        }
                        else if (parameterCount == 3)
                        {
                            decrypted = TryInvokeThreeArgumentDecrypt(target, method, filePath, metadata, originalBytes);
                        }
                        else if (parameterCount == 4)
                        {
                            decrypted = TryInvokeFourArgumentDecrypt(target, method, filePath, metadata, originalBytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[ThreeShapeDecryptor] Method {binding.Type.FullName}.{method.Name} from {binding.AssemblyPath} failed: {ex.Message}");
                    }

                    if (decrypted.Length > 0)
                    {
                        Console.Error.WriteLine($"[ThreeShapeDecryptor] Decrypt succeeded via {binding.Type.FullName}.{method.Name} ({Path.GetFileName(binding.AssemblyPath)}).");
                        return decrypted;
                    }
                }

                Console.Error.WriteLine("[ThreeShapeDecryptor] All decrypt methods returned no usable decrypted data.");
                return Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ThreeShapeDecryptor] Decryption failed: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Bypasses the 3Shape caller-identity check in ThreeShape.FileFormats.HpsAndDcm.dll.
        /// The library's lG class has a one-time security check (lG.CI()) gated by a static
        /// bool field bYY. Setting bYY = true before any instance is created skips the
        /// certificate/process-name verification entirely.
        /// </summary>
        private static void SetupSecurityBypass()
        {
            // Handler that sets the bypass flag as soon as HpsAndDcm is loaded.
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
            {
                try
                {
                    if (args.LoadedAssembly.GetName().Name == "ThreeShape.FileFormats.HpsAndDcm")
                    {
                        TrySetBypassFlag(args.LoadedAssembly);
                    }
                }
                catch
                {
                    // Swallow — never crash due to bypass logic.
                }
            };

            // Also patch any copy that was loaded before this point.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetName().Name == "ThreeShape.FileFormats.HpsAndDcm")
                    {
                        TrySetBypassFlag(asm);
                        break;
                    }
                }
                catch
                {
                    // Swallow.
                }
            }
        }

        private static void TrySetBypassFlag(Assembly hpsDcmAssembly)
        {
            // Obfuscated type: l.lG contains the public-key whitelist and security check.
            // The once-flag bYY gates CI() — setting it to true causes CI() to return
            // immediately without performing the certificate / process-name verification.
            var lgType = hpsDcmAssembly.GetType("l.lG");
            if (lgType is null)
            {
                Console.Error.WriteLine("[ThreeShapeDecryptor] SecurityBypass: type l.lG not found in HpsAndDcm.");
                return;
            }

            var byyField = lgType.GetField("bYY", BindingFlags.Static | BindingFlags.NonPublic);
            if (byyField is null)
            {
                Console.Error.WriteLine("[ThreeShapeDecryptor] SecurityBypass: field bYY not found on l.lG.");
                return;
            }

            byyField.SetValue(null, true);
            Console.Error.WriteLine("[ThreeShapeDecryptor] SecurityBypass: l.lG.bYY set to true — security check bypassed.");
        }

        private static void AttachAssemblyResolverOnce()
        {
            if (_assemblyResolverAttached)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                try
                {
                    var assemblyName = new AssemblyName(args.Name).Name;
                    if (string.IsNullOrWhiteSpace(assemblyName))
                    {
                        return null;
                    }

                    var fileName = assemblyName + ".dll";
                    foreach (var directory in _probingDirectories)
                    {
                        var candidatePath = Path.Combine(directory, fileName);
                        if (File.Exists(candidatePath))
                        {
                            return Assembly.LoadFrom(candidatePath);
                        }
                    }
                }
                catch
                {
                    // Ignore and continue probing.
                }

                return null;
            };

            _assemblyResolverAttached = true;
        }

        private static IEnumerable<string> DiscoverCandidateDlls()
        {
            var requiredFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ThreeShape.DentalSystem.AttachmentDecryptorCom.dll",
                "ThreeShape.DentalSystem.AttachmentDecryption.dll",
                "ThreeShape.DentalSystem.AttachmentDecryptionInterface.dll",
                "ThreeShape.DentalSystem.AttachmentDecrypter.dll",
                "ThreeShape.DentalDesktop.Encryption.dll"
            };

            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var envOverride = Environment.GetEnvironmentVariable("THREESHAPE_DECRYPTOR_DLL");
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                if (File.Exists(envOverride))
                {
                    if (seenPaths.Add(envOverride))
                    {
                        yield return envOverride;
                    }

                    yield break;
                }

                if (Directory.Exists(envOverride))
                {
                    foreach (var path in ProbeCandidateDlls(envOverride, requiredFileNames, seenPaths))
                    {
                        yield return path;
                    }

                    yield break;
                }
            }

            // Search all 3Shape install directories without giving preference to the version
            // from the file's SourceApp metadata. .NET only loads one copy of an assembly by name,
            // so we sort directories newest-first (via EnumerateLikelyProbeDirectories) to ensure
            // the latest version (which has the most complete decrypt API) is loaded first.
            foreach (var baseDirectory in BuildSearchDirectories())
            {
                if (!Directory.Exists(baseDirectory))
                {
                    continue;
                }

                foreach (var path in ProbeCandidateDlls(baseDirectory, requiredFileNames, seenPaths))
                {
                    yield return path;
                }
            }
        }

        private static IEnumerable<string> ProbeCandidateDlls(
            string baseDirectory,
            HashSet<string> requiredFileNames,
            HashSet<string> seenPaths)
        {
            foreach (var directory in EnumerateLikelyProbeDirectories(baseDirectory))
            {
                foreach (var fileName in requiredFileNames)
                {
                    var directPath = Path.Combine(directory, fileName);
                    if (File.Exists(directPath) && seenPaths.Add(directPath))
                    {
                        yield return directPath;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateLikelyProbeDirectories(string baseDirectory)
        {
            if (!Directory.Exists(baseDirectory))
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (seen.Add(baseDirectory))
            {
                yield return baseDirectory;
            }

            IEnumerable<string> childDirectories;
            try
            {
                // Sort newest-first so the latest installed version is loaded first.
                // This ensures e.g. DentalDesigner2024-1 is inspected before DentalDesigner2023-1,
                // which matters because .NET only loads one version of an assembly by name and
                // the newest version is most likely to have the decrypt methods we need.
                childDirectories = Directory.EnumerateDirectories(baseDirectory)
                    .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                yield break;
            }

            foreach (var childDirectory in childDirectories)
            {
                var name = Path.GetFileName(childDirectory);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!name.Contains("Dental", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("3Shape", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seen.Add(childDirectory))
                {
                    yield return childDirectory;
                }
            }
        }

        private static IEnumerable<string> BuildSearchDirectories()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in BuildRootCandidates())
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (seen.Add(path))
                {
                    yield return path;
                }
            }
        }

        private static IEnumerable<string> BuildRootCandidates()
        {
            var roots = new List<string>();
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";

            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                roots.Add(Path.Combine(programFiles, "3Shape"));
            }

            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                roots.Add(Path.Combine(programFilesX86, "3Shape"));
            }

            if (!string.IsNullOrWhiteSpace(commonData))
            {
                roots.Add(Path.Combine(commonData, "3Shape"));
            }

            roots.Add(Path.Combine(systemDrive + Path.DirectorySeparatorChar, "3Shape"));
            roots.Add(Path.Combine(systemDrive + Path.DirectorySeparatorChar, "ProgramData", "3Shape"));
            roots.Add(Path.Combine(systemDrive + Path.DirectorySeparatorChar, "Program Files", "3Shape"));
            roots.Add(Path.Combine(systemDrive + Path.DirectorySeparatorChar, "Program Files (x86)", "3Shape"));

            var envOverride = Environment.GetEnvironmentVariable("THREESHAPE_DECRYPTOR_DLL");
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                if (File.Exists(envOverride))
                {
                    var directory = Path.GetDirectoryName(envOverride);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        roots.Add(directory);
                    }
                }
                else if (Directory.Exists(envOverride))
                {
                    roots.Add(envOverride);
                }
            }

            foreach (var root in roots)
            {
                yield return root;
            }
        }

        private static string? ResolvePreferredInstallDirectory(IReadOnlyDictionary<string, string> metadata)
        {
            if (!metadata.TryGetValue("SourceApp", out var sourceApp) || string.IsNullOrWhiteSpace(sourceApp))
            {
                return null;
            }

            var versionMatch = Regex.Match(sourceApp, @"Dental System\s+(?<version>\d{4}-\d+)", RegexOptions.IgnoreCase);
            var preferredNames = new List<string>();

            if (versionMatch.Success)
            {
                preferredNames.Add("DentalDesigner" + versionMatch.Groups["version"].Value);
            }

            if (sourceApp.Contains("DentalDesigner.exe", StringComparison.OrdinalIgnoreCase))
            {
                preferredNames.Add("DentalDesigner");
            }

            if (sourceApp.Contains("DentalManager", StringComparison.OrdinalIgnoreCase))
            {
                preferredNames.Add("DentalManager");
            }

            foreach (var root in BuildRootCandidates().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var preferredName in preferredNames.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var candidate = Path.Combine(root, preferredName);
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static void TryRegisterAssembly(string assemblyPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(assemblyPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    _probingDirectories.Add(directory);
                }

                var assembly = Assembly.LoadFrom(assemblyPath);

                Type[] publicTypes;
                try
                {
                    publicTypes = assembly.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    Console.Error.WriteLine($"[ThreeShapeDecryptor] Partial type load from {Path.GetFileName(assemblyPath)}: {rtle.Message}");
                    foreach (var le in rtle.LoaderExceptions.Where(e => e != null).Take(3))
                    {
                        Console.Error.WriteLine($"[ThreeShapeDecryptor]   LoaderException: {le!.Message}");
                    }
                    publicTypes = rtle.Types.Where(t => t != null).ToArray()!;
                }

                foreach (var type in publicTypes)
                {
                    if (type.IsInterface || type.IsAbstract)
                    {
                        continue;
                    }

                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                    {
                        if (!method.Name.Contains("Decrypt", StringComparison.OrdinalIgnoreCase) ||
                            !IsSupportedDecryptSignature(method))
                        {
                            continue;
                        }

                        object? instance = null;
                        if (!method.IsStatic)
                        {
                            instance = TryCreateInstance(type);
                            if (instance is null)
                            {
                                Console.Error.WriteLine($"[ThreeShapeDecryptor] Found matching method {type.Name}.{method.Name} but could not instantiate {type.FullName} — skipping.");
                                continue;
                            }
                        }

                        Console.Error.WriteLine($"[ThreeShapeDecryptor] Registered: {type.Name}.{method.Name} (score={ScoreDecryptMethod(method)}) from {Path.GetFileName(assemblyPath)}");
                        _bindings.Add(new DecryptMethodBinding(
                            assemblyPath,
                            type,
                            method,
                            instance,
                            ScoreDecryptMethod(method)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ThreeShapeDecryptor] Failed to inspect {Path.GetFileName(assemblyPath)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool IsSupportedDecryptSignature(MethodInfo method)
        {
            var parameters = method.GetParameters();

            if (parameters.Length == 1)
            {
                var p0 = parameters[0].ParameterType;
                return p0 == typeof(string) || p0 == typeof(byte[]);
            }

            if (parameters.Length == 2)
            {
                return parameters[0].ParameterType == typeof(string) &&
                       parameters[1].ParameterType == typeof(string) &&
                       method.ReturnType == typeof(byte[]);
            }

            if (parameters.Length == 3)
            {
                return parameters[0].ParameterType == typeof(string) &&
                       parameters[1].ParameterType == typeof(string) &&
                       parameters[2].ParameterType == typeof(byte[]).MakeByRefType() &&
                       (method.ReturnType == typeof(bool) || method.ReturnType == typeof(int));
            }

            if (parameters.Length == 4)
            {
                return parameters[0].ParameterType == typeof(string) &&
                       parameters[1].ParameterType == typeof(string) &&
                       parameters[2].ParameterType == typeof(byte[]).MakeByRefType() &&
                       parameters[3].ParameterType == typeof(string).MakeByRefType() &&
                       (method.ReturnType == typeof(bool) || method.ReturnType == typeof(int));
            }

            return false;
        }

        private static int ScoreDecryptMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var score = 0;

            if (parameters.Length == 4)
            {
                score += 120;
            }
            else if (parameters.Length == 3)
            {
                score += 100;
            }
            else if (parameters.Length == 2)
            {
                score += 70;
            }
            else if (parameters.Length == 1)
            {
                score += 50;
            }

            if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
            {
                score += 20;
            }

            if (method.ReturnType == typeof(bool))
            {
                score += 10;
            }

            return score;
        }

        private static object? TryCreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                Console.Error.WriteLine($"[ThreeShapeDecryptor] CreateInstance({type.FullName}) failed: {inner.GetType().Name}: {inner.Message}");
                return null;
            }
        }

        private static byte[] TryInvokeSingleArgumentDecrypt(object? target, MethodInfo method, string filePath)
        {
            var paramType = method.GetParameters()[0].ParameterType;
            object? result;

            if (paramType == typeof(string))
            {
                result = method.Invoke(target, new object[] { filePath });
            }
            else if (paramType == typeof(byte[]))
            {
                var fileContent = File.ReadAllBytes(filePath);
                result = method.Invoke(target, new object[] { fileContent });
            }
            else
            {
                return Array.Empty<byte>();
            }

            if (result is byte[] decrypted)
            {
                Console.Error.WriteLine($"[ThreeShapeDecryptor] Successfully decrypted {decrypted.Length} bytes");
                return decrypted;
            }

            return Array.Empty<byte>();
        }

        private static byte[] TryInvokeTwoArgumentDecrypt(
            object? target,
            MethodInfo method,
            string filePath,
            IReadOnlyDictionary<string, string>? metadata,
            byte[] originalBytes)
        {
            foreach (var password in BuildPasswordCandidates(metadata))
            {
                var result = method.Invoke(target, new object[] { filePath, password });
                if (result is byte[] decrypted && decrypted.Length > 0)
                {
                    if (!IsDifferentFromOriginal(decrypted, originalBytes))
                    {
                        continue;
                    }

                    Console.Error.WriteLine($"[ThreeShapeDecryptor] Decrypt succeeded with password '{password}' ({decrypted.Length} bytes)");
                    return decrypted;
                }
            }

            return Array.Empty<byte>();
        }

        private static byte[] TryInvokeThreeArgumentDecrypt(
            object? target,
            MethodInfo method,
            string filePath,
            IReadOnlyDictionary<string, string>? metadata,
            byte[] originalBytes)
        {
            foreach (var password in BuildPasswordCandidates(metadata))
            {
                var args = new object?[] { filePath, password, null };
                var result = method.Invoke(target, args);
                var success = result is bool b ? b : result is int i && i != 0;
                var decrypted = args[2] as byte[];

                if (success && decrypted is { Length: > 0 })
                {
                    if (!IsDifferentFromOriginal(decrypted, originalBytes))
                    {
                        continue;
                    }

                    Console.Error.WriteLine($"[ThreeShapeDecryptor] Decrypt succeeded with password '{password}' ({decrypted.Length} bytes)");
                    return decrypted;
                }
            }

            return Array.Empty<byte>();
        }

        private static byte[] TryInvokeFourArgumentDecrypt(
            object? target,
            MethodInfo method,
            string filePath,
            IReadOnlyDictionary<string, string>? metadata,
            byte[] originalBytes)
        {
            foreach (var password in BuildPasswordCandidates(metadata))
            {
                var args = new object?[] { filePath, password, null, null };
                var result = method.Invoke(target, args);
                var success = result is bool b ? b : result is int i && i != 0;
                var decrypted = args[2] as byte[];
                var error = args[3] as string;

                if (success && decrypted is { Length: > 0 })
                {
                    if (!IsDifferentFromOriginal(decrypted, originalBytes))
                    {
                        Console.Error.WriteLine($"[ThreeShapeDecryptor] Password '{password}' returned unchanged file bytes; treating as not decrypted.");
                        continue;
                    }

                    Console.Error.WriteLine($"[ThreeShapeDecryptor] Decrypt succeeded with password '{password}' ({decrypted.Length} bytes)");
                    return decrypted;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.Error.WriteLine($"[ThreeShapeDecryptor] Password '{password}' failed: {error}");
                }
            }

            return Array.Empty<byte>();
        }

        private static bool IsDifferentFromOriginal(byte[] candidate, byte[] original)
        {
            if (candidate.Length != original.Length)
            {
                return true;
            }

            return !candidate.AsSpan().SequenceEqual(original);
        }

        private static IEnumerable<string> BuildPasswordCandidates(IReadOnlyDictionary<string, string>? metadata)
        {
            var yielded = new HashSet<string>(StringComparer.Ordinal);
            var exhaustive = string.Equals(
                Environment.GetEnvironmentVariable("THREESHAPE_DECRYPTOR_EXHAUSTIVE"),
                "1",
                StringComparison.OrdinalIgnoreCase);

            if (yielded.Add(string.Empty))
            {
                yield return string.Empty;
            }

            if (yielded.Add("DESS Implants for US (C);"))
            {
                yield return "DESS Implants for US (C);";
            }

            if (yielded.Add("DESS Implants for US (C)"))
            {
                yield return "DESS Implants for US (C)";
            }

            if (metadata is null)
            {
                yield break;
            }

            if (metadata.TryGetValue("PackageLockList", out var packageLock) && !string.IsNullOrWhiteSpace(packageLock))
            {
                if (yielded.Add(packageLock))
                {
                    yield return packageLock;
                }

                var trimmedPackageLock = packageLock.TrimEnd(';');
                if (!string.IsNullOrWhiteSpace(trimmedPackageLock) && yielded.Add(trimmedPackageLock))
                {
                    yield return trimmedPackageLock;
                }

                var canonicalItems = packageLock
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray();

                if (canonicalItems.Length > 0)
                {
                    var canonical = string.Join(';', canonicalItems) + ";";
                    if (yielded.Add(canonical))
                    {
                        yield return canonical;
                    }

                    var canonicalHashBytes = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
                    var canonicalHashUpper = Convert.ToHexString(canonicalHashBytes);
                    if (yielded.Add(canonicalHashUpper))
                    {
                        yield return canonicalHashUpper;
                    }

                    var canonicalHashLower = canonicalHashUpper.ToLowerInvariant();
                    if (yielded.Add(canonicalHashLower))
                    {
                        yield return canonicalHashLower;
                    }
                }

                if (exhaustive)
                {
                    foreach (var token in packageLock.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (yielded.Add(token))
                        {
                            yield return token;
                        }
                    }
                }
            }

            if (metadata.TryGetValue("IntegrityCheck", out var integrity) && !string.IsNullOrWhiteSpace(integrity))
            {
                if (yielded.Add(integrity))
                {
                    yield return integrity;
                }

                var integrityHashBytes = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(integrity));
                var integrityHashUpper = Convert.ToHexString(integrityHashBytes);
                if (yielded.Add(integrityHashUpper))
                {
                    yield return integrityHashUpper;
                }

                var integrityHashLower = integrityHashUpper.ToLowerInvariant();
                if (yielded.Add(integrityHashLower))
                {
                    yield return integrityHashLower;
                }
            }

            if (metadata.TryGetValue("EKID", out var ekid) && !string.IsNullOrWhiteSpace(ekid))
            {
                if (yielded.Add(ekid))
                {
                    yield return ekid;
                }
            }

            if (metadata.TryGetValue("SourceApp", out var sourceApp) && !string.IsNullOrWhiteSpace(sourceApp))
            {
                if (yielded.Add(sourceApp))
                {
                    yield return sourceApp;
                }

                var sourceAppShort = sourceApp.Split(',', '+', ';')[0].Trim();
                if (!string.IsNullOrWhiteSpace(sourceAppShort) && yielded.Add(sourceAppShort))
                {
                    yield return sourceAppShort;
                }

                if (exhaustive)
                {
                    var sourceAppTrimmed = sourceApp.Trim();
                    if (yielded.Add(sourceAppTrimmed))
                    {
                        yield return sourceAppTrimmed;
                    }

                    var sourceAppMd5 = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(sourceAppTrimmed));
                    var sourceAppMd5Upper = Convert.ToHexString(sourceAppMd5);
                    if (yielded.Add(sourceAppMd5Upper))
                    {
                        yield return sourceAppMd5Upper;
                    }

                    var sourceAppMd5Lower = sourceAppMd5Upper.ToLowerInvariant();
                    if (yielded.Add(sourceAppMd5Lower))
                    {
                        yield return sourceAppMd5Lower;
                    }

                    var sourceAppSha1 = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(sourceAppTrimmed));
                    var sourceAppSha1Upper = Convert.ToHexString(sourceAppSha1);
                    if (yielded.Add(sourceAppSha1Upper))
                    {
                        yield return sourceAppSha1Upper;
                    }

                    var sourceAppSha1Lower = sourceAppSha1Upper.ToLowerInvariant();
                    if (yielded.Add(sourceAppSha1Lower))
                    {
                        yield return sourceAppSha1Lower;
                    }

                    foreach (var token in sourceAppTrimmed.Split(';', '#', '+', ',', ' '))
                    {
                        var trimmedToken = token.Trim();
                        if (trimmedToken.Length >= 4 && yielded.Add(trimmedToken))
                        {
                            yield return trimmedToken;
                        }
                    }
                }
            }

            if (metadata.TryGetValue("OrderXMLItem_PrimaryKey", out var primaryKey) && !string.IsNullOrWhiteSpace(primaryKey))
            {
                var trimmedPrimaryKey = primaryKey.Trim();
                if (yielded.Add(trimmedPrimaryKey))
                {
                    yield return trimmedPrimaryKey;
                }

                if (exhaustive)
                {
                    var primaryKeyMd5 = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(trimmedPrimaryKey));
                    var primaryKeyMd5Upper = Convert.ToHexString(primaryKeyMd5);
                    if (yielded.Add(primaryKeyMd5Upper))
                    {
                        yield return primaryKeyMd5Upper;
                    }

                    var primaryKeyMd5Lower = primaryKeyMd5Upper.ToLowerInvariant();
                    if (yielded.Add(primaryKeyMd5Lower))
                    {
                        yield return primaryKeyMd5Lower;
                    }

                    var primaryKeySha1 = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(trimmedPrimaryKey));
                    var primaryKeySha1Upper = Convert.ToHexString(primaryKeySha1);
                    if (yielded.Add(primaryKeySha1Upper))
                    {
                        yield return primaryKeySha1Upper;
                    }

                    var primaryKeySha1Lower = primaryKeySha1Upper.ToLowerInvariant();
                    if (yielded.Add(primaryKeySha1Lower))
                    {
                        yield return primaryKeySha1Lower;
                    }
                }
            }
        }
    }
}
