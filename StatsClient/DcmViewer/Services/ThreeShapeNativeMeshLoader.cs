using System.Reflection;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DCMViewer.Services;

internal static class ThreeShapeNativeMeshLoader
{
    private sealed class PasswordCallback
    {
        private readonly string _password;

        public PasswordCallback(string password)
        {
            _password = password;
        }

        public string Resolve(IReadOnlyDictionary<string, string> _, int __)
        {
            return _password;
        }
    }

    private static readonly object Sync = new();
    private static readonly HashSet<string> ProbingDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static bool _resolverAttached;
    private static bool _securityBypassInitialized;

    public static bool TryExportToStl(
        string filePath,
        IReadOnlyDictionary<string, string>? metadata,
        out string exportedStlPath)
    {
        exportedStlPath = string.Empty;

        try
        {
            SetupSecurityBypass();
            AttachAssemblyResolverOnce();

            foreach (var directory in DiscoverCandidateDirectories(metadata))
            {
                if (TryExportToStlFromDirectory(directory, filePath, metadata, out exportedStlPath))
                {
                    Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Exported {Path.GetFileName(filePath)} via {directory}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Export failed: {ex.Message}");
        }

        return false;
    }

    private static void SetupSecurityBypass()
    {
        lock (Sync)
        {
            if (_securityBypassInitialized)
            {
                return;
            }

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
                }
            };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetName().Name == "ThreeShape.FileFormats.HpsAndDcm")
                    {
                        TrySetBypassFlag(assembly);
                    }
                }
                catch
                {
                }
            }

            _securityBypassInitialized = true;
        }
    }

    private static void TrySetBypassFlag(Assembly hpsDcmAssembly)
    {
        var lgType = hpsDcmAssembly.GetType("l.lG");
        if (lgType is null)
        {
            return;
        }

        var byyField = lgType.GetField("bYY", BindingFlags.Static | BindingFlags.NonPublic);
        if (byyField is null)
        {
            return;
        }

        byyField.SetValue(null, true);
        Console.Error.WriteLine("[ThreeShapeNativeMeshLoader] SecurityBypass enabled (l.lG.bYY=true).");
    }

    private static void AttachAssemblyResolverOnce()
    {
        lock (Sync)
        {
            if (_resolverAttached)
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
                    foreach (var directory in ProbingDirectories)
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
                }

                return null;
            };

            _resolverAttached = true;
        }
    }

    private static IEnumerable<string> DiscoverCandidateDirectories(IReadOnlyDictionary<string, string>? metadata)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requiredAssemblies = new[]
        {
            "ThreeShape.FileFormats.HpsAndDcm.dll",
            "ThreeShape.Geometry3D.DataStructures.dll"
        };

        var preferredDirectories = ResolvePreferredInstallDirectories(metadata).ToArray();
        foreach (var preferredDirectory in preferredDirectories)
        {
            if (!Directory.Exists(preferredDirectory) ||
                !seen.Add(preferredDirectory) ||
                !requiredAssemblies.All(fileName => File.Exists(Path.Combine(preferredDirectory, fileName))))
            {
                continue;
            }

            Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Preferred install directory: {preferredDirectory}");
            yield return preferredDirectory;
        }

        // When SourceApp is present and matched, avoid probing unrelated installs that can load incompatible versions first.
        if (preferredDirectories.Length > 0)
        {
            yield break;
        }

        foreach (var root in BuildRootCandidates())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> matches;
            try
            {
                matches = Directory.EnumerateFiles(root, requiredAssemblies[0], SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var hpsAssemblyPath in matches)
            {
                var directory = Path.GetDirectoryName(hpsAssemblyPath);
                if (string.IsNullOrWhiteSpace(directory) || !seen.Add(directory))
                {
                    continue;
                }

                if (requiredAssemblies.All(fileName => File.Exists(Path.Combine(directory, fileName))))
                {
                    yield return directory;
                }
            }
        }
    }

    private static IEnumerable<string> ResolvePreferredInstallDirectories(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            yield break;
        }

        if (!metadata.TryGetValue("SourceApp", out var sourceApp) || string.IsNullOrWhiteSpace(sourceApp))
        {
            yield break;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programFiles))
        {
            yield break;
        }

        var sanitized = sourceApp.Trim().Trim('"', '\'');
        if (sanitized.Length == 0)
        {
            yield break;
        }

        var root = Path.Combine(programFiles, "3Shape");
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var exact = Path.Combine(root, sanitized);
        if (Directory.Exists(exact) && seen.Add(exact))
        {
            yield return exact;
        }

        foreach (var token in ExtractInstallTokens(sanitized))
        {
            var candidate = Path.Combine(root, token);
            if (Directory.Exists(candidate) && seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        if (seen.Count > 0)
        {
            yield break;
        }

        string Normalize(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }

        var sourceNorm = Normalize(sanitized);
        if (sourceNorm.Length == 0)
        {
            yield break;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (var dir in directories)
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var candidateNorm = Normalize(name);
            if (candidateNorm.Contains(sourceNorm, StringComparison.Ordinal) || sourceNorm.Contains(candidateNorm, StringComparison.Ordinal))
            {
                yield return dir;
            }
        }

        static IEnumerable<string> ExtractInstallTokens(string sourceApp)
        {
            var appRegex = new Regex(@"^(?<app>[^#\+]+?)\.exe#", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var versionRegex = new Regex(@"Dental\s+(?:Manager|System)\s+(?<ver>\d{4}-\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var part in sourceApp.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var appMatch = appRegex.Match(part);
                if (!appMatch.Success)
                {
                    continue;
                }

                var app = appMatch.Groups["app"].Value.Trim();
                if (app.Length == 0)
                {
                    continue;
                }

                if (!app.Equals("DentalDesigner", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var verMatch = versionRegex.Match(part);
                if (verMatch.Success)
                {
                    var version = verMatch.Groups["ver"].Value;
                    if (version.Length > 0)
                    {
                        yield return app + version;
                    }
                }

                yield return app;
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

        return roots;
    }

    private static bool TryExportToStlFromDirectory(
        string directory,
        string filePath,
        IReadOnlyDictionary<string, string>? metadata,
        out string exportedStlPath)
    {
        exportedStlPath = string.Empty;

        try
        {
            ProbingDirectories.Add(directory);

            var hpsAssembly = Assembly.LoadFrom(Path.Combine(directory, "ThreeShape.FileFormats.HpsAndDcm.dll"));
            var dataAssembly = Assembly.LoadFrom(Path.Combine(directory, "ThreeShape.Geometry3D.DataStructures.dll"));
            var relatedAssemblies = new[] { hpsAssembly, dataAssembly };

            var loaderType = FindLoaderType(hpsAssembly);
            if (loaderType is null)
            {
                Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Compatible loader type not found in {directory}.");
                return false;
            }

            var encryptedLoadMethods = FindEncryptedLoadMethods(loaderType).ToArray();
            var loadMethod = FindLoadMethod(loaderType);
            var saveMethod = FindSaveMethod(loaderType);
            if ((loadMethod is null && encryptedLoadMethods.Length == 0) || saveMethod is null)
            {
                Console.Error.WriteLine(
                    $"[ThreeShapeNativeMeshLoader] Missing load/save methods in {directory} (encrypted={encryptedLoadMethods.Length}, hasLoad={loadMethod is not null}, hasSave={saveMethod is not null}).");
                return false;
            }

            var loader = Activator.CreateInstance(loaderType);
            if (loader is null)
            {
                Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Failed to create loader instance in {directory}.");
                return false;
            }

            var requiredModelType = (loadMethod ?? encryptedLoadMethods.First()).GetParameters()[0].ParameterType;

            foreach (var modelInstance in CreateModelInstances(dataAssembly, relatedAssemblies, requiredModelType))
            {
                var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".stl");

                try
                {
                    var loaded = false;

                    foreach (var encryptedLoadMethod in encryptedLoadMethods)
                    {
                        if (TryLoadEncryptedModel(encryptedLoadMethod, modelInstance, filePath, metadata))
                        {
                            loaded = true;
                            break;
                        }
                    }

                    if (!loaded)
                    {
                        if (loadMethod is null)
                        {
                            continue;
                        }

                        loadMethod.Invoke(loader, new[] { modelInstance, filePath });
                    }

                    saveMethod.Invoke(loader, new[] { modelInstance, tempPath });

                    if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 84)
                    {
                        exportedStlPath = tempPath;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Model export via {modelInstance.GetType().FullName} failed: {ex.GetBaseException().Message}");
                }

                TryDeleteFile(tempPath);
            }

            Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] No exportable model instances succeeded in {directory}.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Directory probe failed for {directory}: {ex.GetBaseException().Message}");
        }

        return false;
    }

    private static MethodInfo? FindLoadMethod(Type loaderType)
    {
        return loaderType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(void) &&
                       parameters.Length == 2 &&
                       parameters[1].ParameterType == typeof(string) &&
                       parameters[0].ParameterType.Name.Contains("FacetModel", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static Type? FindLoaderType(Assembly hpsAssembly)
    {
        try
        {
            var direct = hpsAssembly.GetTypes().FirstOrDefault(type => type.FullName == "L.LK");
            if (direct is not null)
            {
                return direct;
            }

            foreach (var type in hpsAssembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                var hasInstanceIo = type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(method =>
                    {
                        var parameters = method.GetParameters();
                        return method.ReturnType == typeof(void) &&
                               parameters.Length == 2 &&
                               parameters[1].ParameterType == typeof(string) &&
                               parameters[0].ParameterType.Name.Contains("FacetModel", StringComparison.OrdinalIgnoreCase);
                    });

                var hasEncryptedIo = type
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Any(method =>
                    {
                        var parameters = method.GetParameters();
                        return method.ReturnType == typeof(bool) &&
                               parameters.Length >= 4 &&
                               parameters.Length <= 5 &&
                               parameters[0].ParameterType.Name.Contains("FacetModel", StringComparison.OrdinalIgnoreCase) &&
                               parameters[1].ParameterType == typeof(string) &&
                               typeof(MulticastDelegate).IsAssignableFrom(parameters[2].ParameterType);
                    });

                if (hasInstanceIo || hasEncryptedIo)
                {
                    return type;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static IEnumerable<MethodInfo> FindEncryptedLoadMethods(Type loaderType)
    {
        return loaderType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(bool) &&
                       parameters.Length >= 4 &&
                       parameters.Length <= 5 &&
                       parameters[0].ParameterType.Name.Contains("FacetModel", StringComparison.OrdinalIgnoreCase) &&
                       parameters[1].ParameterType == typeof(string) &&
                       typeof(MulticastDelegate).IsAssignableFrom(parameters[2].ParameterType);
            });
    }

    private static MethodInfo? FindSaveMethod(Type loaderType)
    {
        return loaderType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(void) &&
                       parameters.Length == 2 &&
                       parameters[1].ParameterType == typeof(string) &&
                       parameters[0].ParameterType.Name.Contains("FacetModel", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static IEnumerable<object> CreateModelInstances(
        Assembly dataAssembly,
        IReadOnlyList<Assembly> relatedAssemblies,
        Type requiredModelType)
    {
        var candidateTypes = dataAssembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .OrderByDescending(type => type.FullName?.Contains("HU", StringComparison.OrdinalIgnoreCase) ?? false)
            .ThenBy(type => type.ContainsGenericParameters)
            .ToList();

        foreach (var candidateType in candidateTypes)
        {
            foreach (var instantiatedType in CloseCandidateType(candidateType, relatedAssemblies, requiredModelType))
            {
                object? instance = null;
                try
                {
                    instance = Activator.CreateInstance(instantiatedType, nonPublic: true);
                }
                catch
                {
                }

                if (instance is not null)
                {
                    yield return instance;
                }
            }
        }
    }

    private static bool TryLoadEncryptedModel(
        MethodInfo method,
        object modelInstance,
        string filePath,
        IReadOnlyDictionary<string, string>? metadata)
    {
        foreach (var password in BuildPasswordCandidates(metadata))
        {
            try
            {
                var parameters = method.GetParameters();
                var callbackDelegate = CreatePasswordDelegate(parameters[2].ParameterType, password);
                if (callbackDelegate is null)
                {
                    continue;
                }

                var args = new object?[parameters.Length];
                args[0] = modelInstance;
                args[1] = filePath;
                args[2] = callbackDelegate;

                for (var index = 3; index < parameters.Length; index++)
                {
                    args[index] = CreateDefaultArgument(parameters[index].ParameterType);
                }

                var result = method.Invoke(null, args);
                if (result is bool success && success)
                {
                    Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Loaded encrypted model with password '{password}'.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ThreeShapeNativeMeshLoader] Encrypted load failed for password '{password}': {ex.GetBaseException().Message}");
            }
        }

        return false;
    }

    private static Delegate? CreatePasswordDelegate(Type delegateType, string password)
    {
        try
        {
            var callback = new PasswordCallback(password);
            var method = typeof(PasswordCallback).GetMethod(nameof(PasswordCallback.Resolve), BindingFlags.Public | BindingFlags.Instance);
            return method is null ? null : Delegate.CreateDelegate(delegateType, callback, method);
        }
        catch
        {
            return null;
        }
    }

    private static object? CreateDefaultArgument(Type parameterType)
    {
        if (!parameterType.IsValueType)
        {
            return null;
        }

        return Activator.CreateInstance(parameterType);
    }

    private static IEnumerable<string> BuildPasswordCandidates(IReadOnlyDictionary<string, string>? metadata)
    {
        var yielded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in new[] { string.Empty, "DESS Implants for US (C);", "DESS Implants for US (C)" })
        {
            if (yielded.Add(value))
            {
                yield return value;
            }
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

            var trimmed = packageLock.TrimEnd(';');
            if (!string.IsNullOrWhiteSpace(trimmed) && yielded.Add(trimmed))
            {
                yield return trimmed;
            }

            var canonicalItems = packageLock
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

            if (canonicalItems.Length > 0)
            {
                var canonical = string.Join(';', canonicalItems) + ";";
                foreach (var candidate in new[]
                {
                    canonical,
                    ComputeMd5Hex(canonical, lowerCase: false),
                    ComputeMd5Hex(canonical, lowerCase: true)
                })
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            foreach (var token in packageLock.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (yielded.Add(token))
                {
                    yield return token;
                }
            }
        }

        foreach (var key in new[] { "IntegrityCheck", "EKID", "SourceApp" })
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && yielded.Add(value))
            {
                yield return value;
            }
        }

        if (metadata.TryGetValue("IntegrityCheck", out var integrity) && !string.IsNullOrWhiteSpace(integrity))
        {
            foreach (var candidate in new[]
            {
                ComputeMd5Hex(integrity, lowerCase: false),
                ComputeMd5Hex(integrity, lowerCase: true)
            })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<Type> CloseCandidateType(
        Type candidateType,
        IReadOnlyList<Assembly> relatedAssemblies,
        Type requiredModelType)
    {
        if (!candidateType.ContainsGenericParameters)
        {
            if (requiredModelType.IsAssignableFrom(candidateType))
            {
                yield return candidateType;
            }

            yield break;
        }

        if (!candidateType.IsGenericTypeDefinition)
        {
            yield break;
        }

        var genericParameters = candidateType.GetGenericArguments();
        var candidateLists = genericParameters
            .Select(parameter => BuildGenericArgumentCandidates(parameter, relatedAssemblies).Take(8).ToArray())
            .ToArray();

        if (candidateLists.Any(list => list.Length == 0))
        {
            yield break;
        }

        var emitted = 0;
        foreach (var combination in BuildTypeCombinations(candidateLists, 0, new Type[genericParameters.Length]))
        {
            if (emitted++ >= 32)
            {
                yield break;
            }

            Type? closedType = null;
            try
            {
                closedType = candidateType.MakeGenericType(combination);
            }
            catch
            {
            }

            if (closedType is not null && !closedType.IsAbstract && requiredModelType.IsAssignableFrom(closedType))
            {
                yield return closedType;
            }
        }
    }

    private static IEnumerable<Type[]> BuildTypeCombinations(Type[][] candidateLists, int index, Type[] current)
    {
        if (index >= candidateLists.Length)
        {
            yield return current.ToArray();
            yield break;
        }

        foreach (var candidate in candidateLists[index])
        {
            current[index] = candidate;
            foreach (var combination in BuildTypeCombinations(candidateLists, index + 1, current))
            {
                yield return combination;
            }
        }
    }

    private static IEnumerable<Type> BuildGenericArgumentCandidates(Type genericParameter, IReadOnlyList<Assembly> relatedAssemblies)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var primitiveType in new[] { typeof(float), typeof(double), typeof(int), typeof(uint), typeof(short), typeof(ushort) })
        {
            if (IsGenericArgumentCompatible(genericParameter, primitiveType) && seen.Add(primitiveType.AssemblyQualifiedName!))
            {
                yield return primitiveType;
            }
        }

        var namedCandidates = relatedAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => type.IsValueType || type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderByDescending(type =>
                type.Name.Contains("Point", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Vertex", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Facet", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Triangle", StringComparison.OrdinalIgnoreCase))
            .ThenBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var candidateType in namedCandidates)
        {
            if (!IsGenericArgumentCompatible(genericParameter, candidateType))
            {
                continue;
            }

            if (seen.Add(candidateType.AssemblyQualifiedName!))
            {
                yield return candidateType;
            }
        }
    }

    private static bool IsGenericArgumentCompatible(Type genericParameter, Type candidateType)
    {
        var attributes = genericParameter.GenericParameterAttributes;

        if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && candidateType.IsValueType)
        {
            return false;
        }

        if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint) &&
            (!candidateType.IsValueType || Nullable.GetUnderlyingType(candidateType) is not null))
        {
            return false;
        }

        if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) &&
            !candidateType.IsValueType && candidateType.GetConstructor(Type.EmptyTypes) is null)
        {
            return false;
        }

        foreach (var constraint in genericParameter.GetGenericParameterConstraints())
        {
            if (!constraint.IsAssignableFrom(candidateType))
            {
                return false;
            }
        }

        return true;
    }

    private static string ComputeMd5Hex(string value, bool lowerCase)
    {
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        var hex = Convert.ToHexString(hash);
        return lowerCase ? hex.ToLowerInvariant() : hex;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
