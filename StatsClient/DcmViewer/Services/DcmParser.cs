using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml;
using System.Xml.Linq;
using HelixToolkit.Wpf;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace DCMViewer.Services;

public enum CoordinateDecodingMode
{
    Absolute,
    Delta,
    Auto
}

public sealed record ParsedMeshData(
    MeshGeometry3D Mesh,
    Rect3D Bounds,
    int VertexCount,
    int TriangleCount,
    IReadOnlyDictionary<string, string> Properties,
    bool IsEncrypted,
    PointCollection? TextureCoordinates = null,
    byte[]? TextureImageBytes = null);

public sealed class DcmParser
{
    // Correct CE Blowfish key from kE() constructor in ThreeShape.FileFormats.HpsAndDcm.dll
    // bytes: [52,144,2,147,88,47,73,148,118,2,25,223,59,86,68,28] encoded as ISO-8859-1 string
    private const string BaseCeKeyBase64 = "NJACk1gvSZR2AhnfO1ZEHA==";
    private const int PointStrideBytes = 12;
    private const uint AdlerMod = 65521;

    public ParsedMeshData ParseFile(
        string filePath,
        CoordinateDecodingMode mode = CoordinateDecodingMode.Auto,
        bool allowThreeShapeFallback = true)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The selected file does not exist.", filePath);
        }

        // Handle STL files separately
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".stl")
        {
            return ParseStlFile(filePath);
        }

        var metadata = ReadMetadata(filePath);
        var positions = new List<Point3D>();
        var triangleIndices = new List<int>();
        var foundGeometry = false;
        List<Point3D>? pendingVertices = null;
        PendingFacets? pendingFacets = null;
        var insideCeDepth = 0;
        var hasCeVertices = false;
        var isEncrypted = hasCeVertices || metadata.Properties.ContainsKey("PackageLockList");

        using var stream = File.OpenRead(filePath);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit
        });

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Name.Equals("Vertices", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"[ParseFile-XmlReader] Found Vertices element, insideCeDepth={insideCeDepth}");
                }
                
                if (reader.Name.Equals("CE", StringComparison.OrdinalIgnoreCase))
                {
                    isEncrypted = true;
                    insideCeDepth++;
                    if (!reader.IsEmptyElement)
                    {
                        continue;
                    }

                    insideCeDepth--;
                    continue;
                }
            }

            if (reader.NodeType == XmlNodeType.EndElement &&
                reader.Name.Equals("CE", StringComparison.OrdinalIgnoreCase))
            {
                if (insideCeDepth > 0)
                {
                    insideCeDepth--;
                }

                continue;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            var effectiveSchema = insideCeDepth > 0 ? "CE" : metadata.Schema;

            if (reader.Name.Equals("Vertices", StringComparison.OrdinalIgnoreCase))
            {
                var vertexCount = ReadOptionalIntAttribute(reader, "vertex_count") ?? ReadOptionalIntAttribute(reader, "Count") ?? 0;
                var checkValue = ReadOptionalUIntAttribute(reader, "check_value");
                var (bytes, skipDecryption) = ReadVerticesElementBytes(reader, vertexCount);
                Console.Error.WriteLine($"[Vertices] Count={vertexCount}, BytesLength={bytes.Length}, SkipDecryption={skipDecryption}");
                if (insideCeDepth > 0)
                {
                    hasCeVertices = true;
                }

                try
                {
                    pendingVertices = DecodeVertices(bytes, vertexCount, checkValue, effectiveSchema, metadata.Properties, mode, skipDecryption);
                    foundGeometry = pendingVertices.Count > 0;
                }
                catch (InvalidDataException)
                {
                    pendingVertices = null;
                }

                if (pendingVertices is { Count: > 0 } && pendingFacets is not null)
                {
                    var pendingFaces = DecodeFacets(pendingFacets.Value.Bytes, pendingFacets.Value.FaceCount, pendingVertices.Count, pendingVertices);
                    AppendIndexedMesh(positions, triangleIndices, pendingVertices, pendingFaces);
                    pendingVertices = null;
                    pendingFacets = null;
                    foundGeometry = true;
                }

                continue;
            }

            if (reader.Name.Equals("Facets", StringComparison.OrdinalIgnoreCase))
            {
                var faceCount = ReadRequiredIntAttribute(reader, "facet_count");
                var bytes = ReadFacetsElementBytes(reader, faceCount);
                pendingFacets = new PendingFacets(bytes, faceCount);

                if (pendingVertices is { Count: > 0 })
                {
                    var pendingFaces = DecodeFacets(pendingFacets.Value.Bytes, pendingFacets.Value.FaceCount, pendingVertices.Count, pendingVertices);
                    AppendIndexedMesh(positions, triangleIndices, pendingVertices, pendingFaces);
                    pendingVertices = null;
                    pendingFacets = null;
                    foundGeometry = true;
                }

                continue;
            }
        }

        if ((positions.Count == 0 || triangleIndices.Count == 0) &&
            TryPopulateGeometryFromDocument(filePath, metadata, mode, positions, triangleIndices))
        {
            foundGeometry = positions.Count > 0 && triangleIndices.Count > 0;
        }

        var embeddedPositions = new List<Point3D>();
        var embeddedTriangleIndices = new List<int>();
        if (TryPopulateGeometryFromEmbeddedHps(filePath, mode, embeddedPositions, embeddedTriangleIndices, metadata.Properties))
        {
            var currentScore = ScoreMeshGeometry(positions.Count, triangleIndices.Count / 3, ComputeBounds(positions));
            var embeddedScore = ScoreMeshGeometry(embeddedPositions.Count, embeddedTriangleIndices.Count / 3, ComputeBounds(embeddedPositions));

            if (embeddedScore > currentScore)
            {
                positions.Clear();
                triangleIndices.Clear();
                positions.AddRange(embeddedPositions);
                triangleIndices.AddRange(embeddedTriangleIndices);
                foundGeometry = positions.Count > 0 && triangleIndices.Count > 0;
            }
        }

        var hasGeometry = foundGeometry && positions.Count > 0 && triangleIndices.Count > 0;

        var hasIntegrityCheck = metadata.Properties.ContainsKey("IntegrityCheck");
        var hasEKID = metadata.Properties.ContainsKey("EKID");
        var hasPackageLock = metadata.Properties.ContainsKey("PackageLockList");
        var hasThreeShapeSourceApp = metadata.Properties.TryGetValue("SourceApp", out var sourceAppValue) &&
            (sourceAppValue.Contains("3Shape", StringComparison.OrdinalIgnoreCase) ||
             sourceAppValue.Contains("DentalDesigner", StringComparison.OrdinalIgnoreCase) ||
             sourceAppValue.Contains("DentalManager", StringComparison.OrdinalIgnoreCase));
        var is3ShapeEncrypted = hasIntegrityCheck || hasPackageLock || hasEKID || hasThreeShapeSourceApp;

        var enableNativeLoader = string.Equals(
            Environment.GetEnvironmentVariable("DCMVIEWER_ENABLE_NATIVE_LOADER"),
            "1",
            StringComparison.OrdinalIgnoreCase);
        var forceNativeFallback = string.Equals(
            Environment.GetEnvironmentVariable("DCMVIEWER_FORCE_NATIVE_FALLBACK"),
            "1",
            StringComparison.OrdinalIgnoreCase);
        var shouldTryNativeLoader =
            enableNativeLoader &&
            allowThreeShapeFallback &&
            (!hasGeometry || forceNativeFallback);

        if (enableNativeLoader && hasGeometry && !forceNativeFallback)
        {
            Console.Error.WriteLine("[DcmParser] Skipping native mesh fallback because preview geometry is already available. Set DCMVIEWER_FORCE_NATIVE_FALLBACK=1 to force it.");
        }

        if (shouldTryNativeLoader &&
            ThreeShapeNativeMeshLoader.TryExportToStl(filePath, metadata.Properties, out var nativeStlPath))
        {
            try
            {
                var nativeResult = ParseStlFile(nativeStlPath);
                var currentTriangleCount = triangleIndices.Count / 3;
                if (nativeResult.TriangleCount > currentTriangleCount)
                {
                    return new ParsedMeshData(
                        nativeResult.Mesh,
                        nativeResult.Bounds,
                        nativeResult.VertexCount,
                        nativeResult.TriangleCount,
                        new Dictionary<string, string>(metadata.Properties, StringComparer.OrdinalIgnoreCase),
                        isEncrypted,
                        TextureCoordinates: null,
                        TextureImageBytes: null);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DcmParser] Native 3Shape mesh fallback failed: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (File.Exists(nativeStlPath))
                    {
                        File.Delete(nativeStlPath);
                    }
                }
                catch
                {
                }
            }
        }

        if (is3ShapeEncrypted)
        {
            ThreeShapeDecryptor.ConfigurePreferredInstall(metadata.Properties);
        }

        var forceThreeShapeDecrypt = string.Equals(
            Environment.GetEnvironmentVariable("DCMVIEWER_FORCE_3SHAPE_DECRYPT"),
            "1",
            StringComparison.OrdinalIgnoreCase);
        var shouldTryThreeShapeDecrypt =
            allowThreeShapeFallback &&
            is3ShapeEncrypted &&
            (!hasGeometry || forceThreeShapeDecrypt);

        if (allowThreeShapeFallback && is3ShapeEncrypted && hasGeometry && !forceThreeShapeDecrypt)
        {
            System.Console.Error.WriteLine("[DcmParser] Skipping 3Shape decrypt fallback because preview geometry is already available. Set DCMVIEWER_FORCE_3SHAPE_DECRYPT=1 to force it.");
        }

        if (shouldTryThreeShapeDecrypt && ThreeShapeDecryptor.IsAvailable())
        {
            System.Console.Error.WriteLine("[DcmParser] Attempting 3Shape decryption...");
            try
            {
                var decrypted = ThreeShapeDecryptor.TryDecryptFile(filePath, metadata.Properties);
                if (decrypted.Length > 0)
                {
                    var tempPath = Path.GetTempFileName() + ".dcm";
                    try
                    {
                        File.WriteAllBytes(tempPath, decrypted);
                        var decryptedResult = ParseFile(tempPath, mode, allowThreeShapeFallback: false);
                        var currentTriangleCount = triangleIndices.Count / 3;

                        var currentGeometryScore = ScoreMeshGeometry(positions.Count, currentTriangleCount, ComputeBounds(positions));
                        var decryptedGeometryScore = ScoreMeshGeometry(
                            decryptedResult.VertexCount,
                            decryptedResult.TriangleCount,
                            decryptedResult.Bounds);

                        if (decryptedGeometryScore > currentGeometryScore)
                        {
                            System.Console.Error.WriteLine(
                                $"[DcmParser] 3Shape decrypted parse selected: score {currentGeometryScore:F2} -> {decryptedGeometryScore:F2}");
                            return decryptedResult;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.Error.WriteLine($"[DcmParser] Re-parse after 3Shape decryption failed: {ex.Message}");
                    }
                    finally
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"[DcmParser] 3Shape decryption attempt failed: {ex.Message}");
            }
        }

        if (!foundGeometry || positions.Count == 0 || triangleIndices.Count == 0)
        {
            if (is3ShapeEncrypted)
            {
                var sourceApp = metadata.Properties.TryGetValue("SourceApp", out var app) ? app : "3Shape";
                throw new InvalidDataException(
                    $"This file is encrypted with 3Shape's proprietary encryption. " +
                    $"Requires 3Shape application or proper decryption keys. " +
                    $"(Source: {sourceApp})");
            }
            
            throw new InvalidDataException("No supported mesh payload was found. Expected 3Shape geometry nodes with decryptable <Vertices> data and decodable <Facets> data.");
        }

        TryApplyBestCoordinateTransform(filePath, positions);

        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection(positions),
            TriangleIndices = new Int32Collection(triangleIndices)
        };

        PointCollection? textureCoordinates = null;
        byte[]? textureImageBytes = null;

        mesh.Freeze();

        var triangleCount = triangleIndices.Count / 3;
        return new ParsedMeshData(
            mesh,
            mesh.Bounds,
            mesh.Positions.Count,
            triangleCount,
            new Dictionary<string, string>(metadata.Properties, StringComparer.OrdinalIgnoreCase),
            isEncrypted,
            textureCoordinates,
            textureImageBytes);
    }

    private ParsedMeshData ParseStlFile(string filePath)
    {
        var positions = new List<Point3D>();
        var triangleIndices = new List<int>();

        // Try binary STL first
        if (TryParseStlBinary(filePath, positions, triangleIndices))
        {
            if (positions.Count == 0 || triangleIndices.Count == 0)
            {
                throw new InvalidDataException("STL file contains no geometry.");
            }

            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection(positions),
                TriangleIndices = new Int32Collection(triangleIndices)
            };
            mesh.Freeze();

            return new ParsedMeshData(
                mesh,
                mesh.Bounds,
                mesh.Positions.Count,
                triangleIndices.Count / 3,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                IsEncrypted: false,
                TextureCoordinates: null,
                TextureImageBytes: null);
        }

        throw new InvalidDataException("Failed to parse STL file.");
    }

    private static void TryReadTextureData(
        string filePath,
        int vertexCount,
        IReadOnlyList<int> triangleIndices,
        out PointCollection? textureCoordinates,
        out byte[]? textureImageBytes)
    {
        textureCoordinates = null;
        textureImageBytes = null;

        try
        {
            var document = XDocument.Load(filePath, LoadOptions.None);
            if (!TryDecodeTextureCoordinates(document, vertexCount, triangleIndices, out var decodedTextureCoordinates))
            {
                return;
            }

            if (!TryDecodeTextureImage(document, out var decodedImageBytes))
            {
                return;
            }

            textureCoordinates = decodedTextureCoordinates;
            textureImageBytes = decodedImageBytes;
        }
        catch
        {
            textureCoordinates = null;
            textureImageBytes = null;
        }
    }

    private static bool TryDecodeTextureCoordinates(
        XDocument document,
        int vertexCount,
        IReadOnlyList<int> triangleIndices,
        out PointCollection? textureCoordinates)
    {
        textureCoordinates = null;

        var textureNode = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("PerVertexTextureCoord", StringComparison.OrdinalIgnoreCase));
        if (textureNode is null)
        {
            return false;
        }

        var payload = textureNode.Value?.Trim();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bytes.Length < 6 || vertexCount <= 0)
        {
            return false;
        }

        var requiredBytes = vertexCount * 6;
        if (bytes.Length < requiredBytes)
        {
            return false;
        }

        var availableSlack = bytes.Length - requiredBytes;
        var bestOffset = 0;
        var bestMode = UvDecodeMode.LittleEndianFlipV;
        var bestScore = double.PositiveInfinity;
        var bestAbsoluteOffset = 0;
        var bestAbsoluteMode = UvDecodeMode.LittleEndianFlipV;
        var bestAbsoluteScore = double.PositiveInfinity;
        const int maxCalibrationVertices = 50000;

        var maxOffsetToTest = availableSlack - (availableSlack % 6);
        var offsetStep = DetermineUvOffsetStep(maxOffsetToTest);
        var calibrationVertexCount = Math.Min(vertexCount, maxCalibrationVertices);

        for (var offset = 0; offset <= maxOffsetToTest; offset += offsetStep)
        {
            foreach (var mode in new[]
                     {
                         UvDecodeMode.LittleEndianFlipV,
                         UvDecodeMode.LittleEndian,
                         UvDecodeMode.BigEndianFlipV,
                         UvDecodeMode.BigEndian,
                         UvDecodeMode.LittleEndianFlipVDelta,
                         UvDecodeMode.LittleEndianDelta,
                         UvDecodeMode.BigEndianFlipVDelta,
                         UvDecodeMode.BigEndianDelta,
                         UvDecodeMode.LittleEndianFlipVPredictive,
                         UvDecodeMode.LittleEndianPredictive,
                         UvDecodeMode.BigEndianFlipVPredictive,
                         UvDecodeMode.BigEndianPredictive
                     })
            {
                var decoded = DecodeUvSet(bytes, offset, calibrationVertexCount, mode);
                var score = ScoreUvLayout(decoded, triangleIndices);
                if (IsAbsoluteUvMode(mode) && score < bestAbsoluteScore)
                {
                    bestAbsoluteScore = score;
                    bestAbsoluteOffset = offset;
                    bestAbsoluteMode = mode;
                }

                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestOffset = offset;
                bestMode = mode;
            }
        }

        var refineStart = Math.Max(0, bestOffset - offsetStep);
        var refineEnd = Math.Min(maxOffsetToTest, bestOffset + offsetStep);
        for (var offset = refineStart; offset <= refineEnd; offset += 6)
        {
            foreach (var mode in new[]
                     {
                         UvDecodeMode.LittleEndianFlipV,
                         UvDecodeMode.LittleEndian,
                         UvDecodeMode.BigEndianFlipV,
                         UvDecodeMode.BigEndian,
                         UvDecodeMode.LittleEndianFlipVDelta,
                         UvDecodeMode.LittleEndianDelta,
                         UvDecodeMode.BigEndianFlipVDelta,
                         UvDecodeMode.BigEndianDelta,
                         UvDecodeMode.LittleEndianFlipVPredictive,
                         UvDecodeMode.LittleEndianPredictive,
                         UvDecodeMode.BigEndianFlipVPredictive,
                         UvDecodeMode.BigEndianPredictive
                     })
            {
                var decoded = DecodeUvSet(bytes, offset, calibrationVertexCount, mode);
                var score = ScoreUvLayout(decoded, triangleIndices);
                if (IsAbsoluteUvMode(mode) && score < bestAbsoluteScore)
                {
                    bestAbsoluteScore = score;
                    bestAbsoluteOffset = offset;
                    bestAbsoluteMode = mode;
                }

                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestOffset = offset;
                bestMode = mode;
            }
        }

        if (double.IsPositiveInfinity(bestScore))
        {
            return false;
        }

        if (double.IsFinite(bestAbsoluteScore) && bestAbsoluteScore <= (bestScore * 1.12))
        {
            bestScore = bestAbsoluteScore;
            bestOffset = bestAbsoluteOffset;
            bestMode = bestAbsoluteMode;
        }

        textureCoordinates = DecodeUvSet(bytes, bestOffset, vertexCount, bestMode);
        return true;
    }

    private static bool IsAbsoluteUvMode(UvDecodeMode mode)
        => mode is UvDecodeMode.LittleEndian or
               UvDecodeMode.LittleEndianFlipV or
               UvDecodeMode.BigEndian or
               UvDecodeMode.BigEndianFlipV;

    private static int DetermineUvOffsetStep(int maxOffsetToTest)
    {
        const int minStep = 6;
        const int maxOffsetCandidates = 72;

        if (maxOffsetToTest <= 0)
        {
            return minStep;
        }

        var totalCandidates = (maxOffsetToTest / minStep) + 1;
        if (totalCandidates <= maxOffsetCandidates)
        {
            return minStep;
        }

        var factor = (int)Math.Ceiling((double)totalCandidates / maxOffsetCandidates);
        return factor * minStep;
    }

    private static double ScoreUvLayout(
        PointCollection coordinates,
        IReadOnlyList<int> triangleIndices)
    {
        if (triangleIndices.Count < 3)
        {
            return double.PositiveInfinity;
        }

        const int maxTrianglesToSample = 9000;
        var totalTriangles = triangleIndices.Count / 3;
        var sampledTriangles = Math.Min(totalTriangles, maxTrianglesToSample);
        if (sampledTriangles <= 0)
        {
            return double.PositiveInfinity;
        }

        var triangleStep = Math.Max(totalTriangles / sampledTriangles, 1);

        var edgeDeltaSum = 0.0;
        var edgeCount = 0;

        for (var sampled = 0; sampled < sampledTriangles; sampled++)
        {
            var triangleIndex = Math.Min(sampled * triangleStep, totalTriangles - 1);
            var i = triangleIndex * 3;
            var a = triangleIndices[i];
            var b = triangleIndices[i + 1];
            var c = triangleIndices[i + 2];

            if (a < 0 || b < 0 || c < 0 || a >= coordinates.Count || b >= coordinates.Count || c >= coordinates.Count)
            {
                continue;
            }

            var av = coordinates[a];
            var bv = coordinates[b];
            var cv = coordinates[c];

            edgeDeltaSum += WrappedDistance(av.X, av.Y, bv.X, bv.Y);
            edgeDeltaSum += WrappedDistance(bv.X, bv.Y, cv.X, cv.Y);
            edgeDeltaSum += WrappedDistance(cv.X, cv.Y, av.X, av.Y);
            edgeCount += 3;
        }

        return edgeCount == 0 ? double.PositiveInfinity : edgeDeltaSum / edgeCount;
    }

    private static PointCollection DecodeUvSet(byte[] bytes, int offset, int vertexCount, UvDecodeMode mode)
    {
        var points = new PointCollection(vertexCount);

        switch (mode)
        {
            case UvDecodeMode.LittleEndian:
            case UvDecodeMode.LittleEndianFlipV:
            case UvDecodeMode.BigEndian:
            case UvDecodeMode.BigEndianFlipV:
                for (var i = 0; i < vertexCount; i++)
                {
                    DecodeUvAbsolute(bytes, offset, i, mode, out var u, out var v);
                    points.Add(new Point(u, v));
                }
                break;
            case UvDecodeMode.LittleEndianDelta:
            case UvDecodeMode.LittleEndianFlipVDelta:
            case UvDecodeMode.BigEndianDelta:
            case UvDecodeMode.BigEndianFlipVDelta:
                DecodeUvDeltaLike(bytes, offset, vertexCount, mode, points, predictive: false);
                break;
            case UvDecodeMode.LittleEndianPredictive:
            case UvDecodeMode.LittleEndianFlipVPredictive:
            case UvDecodeMode.BigEndianPredictive:
            case UvDecodeMode.BigEndianFlipVPredictive:
                DecodeUvDeltaLike(bytes, offset, vertexCount, mode, points, predictive: true);
                break;
        }

        return points;
    }

    private static void DecodeUvDeltaLike(
        byte[] bytes,
        int offset,
        int vertexCount,
        UvDecodeMode mode,
        PointCollection points,
        bool predictive)
    {
        var littleEndian = mode is
            UvDecodeMode.LittleEndianDelta or
            UvDecodeMode.LittleEndianFlipVDelta or
            UvDecodeMode.LittleEndianPredictive or
            UvDecodeMode.LittleEndianFlipVPredictive;

        var flipV = mode is
            UvDecodeMode.LittleEndianFlipVDelta or
            UvDecodeMode.BigEndianFlipVDelta or
            UvDecodeMode.LittleEndianFlipVPredictive or
            UvDecodeMode.BigEndianFlipVPredictive;

        var prevU = 0.0;
        var prevV = 0.0;
        var prevPrevU = 0.0;
        var prevPrevV = 0.0;

        for (var i = 0; i < vertexCount; i++)
        {
            var p = offset + (i * 6);
            var uRaw = littleEndian
                ? bytes[p] | (bytes[p + 1] << 8) | (bytes[p + 2] << 16)
                : bytes[p + 2] | (bytes[p + 1] << 8) | (bytes[p] << 16);

            var vRaw = littleEndian
                ? bytes[p + 3] | (bytes[p + 4] << 8) | (bytes[p + 5] << 16)
                : bytes[p + 5] | (bytes[p + 4] << 8) | (bytes[p + 3] << 16);

            var du = uRaw / 16777215.0;
            var dv = vRaw / 16777215.0;
            if (du > 0.5)
            {
                du -= 1.0;
            }

            if (dv > 0.5)
            {
                dv -= 1.0;
            }

            var baseU = predictive && i > 0 ? ((2 * prevU) - prevPrevU) : prevU;
            var baseV = predictive && i > 0 ? ((2 * prevV) - prevPrevV) : prevV;

            var u = WrapUnit(baseU + du);
            var v = WrapUnit(baseV + dv);

            if (flipV)
            {
                v = 1.0 - v;
            }

            points.Add(new Point(u, v));

            prevPrevU = prevU;
            prevPrevV = prevV;
            prevU = u;
            prevV = v;
        }
    }

    private static void DecodeUvAbsolute(byte[] bytes, int offset, int vertexIndex, UvDecodeMode mode, out double u, out double v)
    {
        var p = offset + (vertexIndex * 6);
        var littleEndian = mode is UvDecodeMode.LittleEndian or UvDecodeMode.LittleEndianFlipV;
        var flipV = mode is UvDecodeMode.LittleEndianFlipV or UvDecodeMode.BigEndianFlipV;

        var uRaw = littleEndian
            ? bytes[p] | (bytes[p + 1] << 8) | (bytes[p + 2] << 16)
            : bytes[p + 2] | (bytes[p + 1] << 8) | (bytes[p] << 16);

        var vRaw = littleEndian
            ? bytes[p + 3] | (bytes[p + 4] << 8) | (bytes[p + 5] << 16)
            : bytes[p + 5] | (bytes[p + 4] << 8) | (bytes[p + 3] << 16);

        u = uRaw / 16777215.0;
        v = vRaw / 16777215.0;
        if (flipV)
        {
            v = 1.0 - v;
        }
    }

    private static double WrapUnit(double value)
    {
        var wrapped = value - Math.Floor(value);
        return wrapped >= 1.0 ? 0.0 : wrapped;
    }

    private static double WrappedDistance(double u1, double v1, double u2, double v2)
    {
        var du = Math.Abs(u1 - u2);
        var dv = Math.Abs(v1 - v2);
        du = Math.Min(du, 1.0 - du);
        dv = Math.Min(dv, 1.0 - dv);
        return du + dv;
    }

    private enum UvDecodeMode
    {
        LittleEndian,
        LittleEndianFlipV,
        BigEndian,
        BigEndianFlipV,
        LittleEndianDelta,
        LittleEndianFlipVDelta,
        BigEndianDelta,
        BigEndianFlipVDelta,
        LittleEndianPredictive,
        LittleEndianFlipVPredictive,
        BigEndianPredictive,
        BigEndianFlipVPredictive
    }

    private static bool TryDecodeTextureImage(XDocument document, out byte[]? imageBytes)
    {
        imageBytes = null;

        var textureImageNode = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("TextureImage", StringComparison.OrdinalIgnoreCase));
        if (textureImageNode is null)
        {
            return false;
        }

        var payload = textureImageNode.Value?.Trim();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            imageBytes = Convert.FromBase64String(payload);
            return imageBytes.Length > 0;
        }
        catch (FormatException)
        {
            imageBytes = null;
            return false;
        }
    }

    private bool TryParseStlBinary(string filePath, List<Point3D> positions, List<int> triangleIndices)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            // Skip 80-byte header
            if (stream.Length < 84)
            {
                return false;
            }

            stream.Seek(80, SeekOrigin.Begin);

            // Read triangle count
            var triangleCount = reader.ReadUInt32();
            if (triangleCount == 0)
            {
                return false;
            }

            // Expected size: header (80) + count (4) + triangles (50 each)
            var expectedSize = 80 + 4 + (triangleCount * 50);
            if (stream.Length < expectedSize)
            {
                return false;
            }

            var vertexIndexMap = new Dictionary<Point3D, int>();

            for (var i = 0; i < triangleCount; i++)
            {
                // Read normal (12 bytes, 3 floats) - we skip this
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();

                // Read 3 vertices (36 bytes, 9 floats)
                var v0 = new Point3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var v1 = new Point3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var v2 = new Point3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                // Skip attribute byte count (2 bytes)
                reader.ReadUInt16();

                // Add vertices to list, reusing existing indices
                var indices = new int[3];
                foreach (var (j, vertex) in new[] { v0, v1, v2 }.Select((v, index) => (index, v)))
                {
                    if (!vertexIndexMap.TryGetValue(vertex, out var index))
                    {
                        index = positions.Count;
                        positions.Add(vertex);
                        vertexIndexMap[vertex] = index;
                    }
                    indices[j] = index;
                }

                triangleIndices.Add(indices[0]);
                triangleIndices.Add(indices[1]);
                triangleIndices.Add(indices[2]);
            }

            return positions.Count > 0 && triangleIndices.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static DcmMetadata ReadMetadata(string filePath)
    {
        string? schema = null;
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var stream = File.OpenRead(filePath);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit
        });

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Name.Equals("Schema", StringComparison.OrdinalIgnoreCase))
            {
                schema = reader.ReadElementContentAsString().Trim();
                continue;
            }

            if (!reader.Name.Equals("Property", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = reader.GetAttribute("name");
            var value = reader.GetAttribute("value");
            if (!string.IsNullOrWhiteSpace(name) && value is not null)
            {
                properties[name] = value;
            }
        }

        return new DcmMetadata(schema ?? string.Empty, properties);
    }

    private static byte[] ReadBase64ElementBytes(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return Array.Empty<byte>();
        }

        var content = reader.ReadElementContentAsString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<byte>();
        }

        return Convert.FromBase64String(content.Trim());
    }

    private static (byte[] Bytes, bool SkipCeDecryption) ReadVerticesElementBytes(XmlReader reader, int vertexCount)
    {
        Console.Error.WriteLine($"[ReadVerticesElementBytes] Called with vertexCount={vertexCount}");
        
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return (Array.Empty<byte>(), false);
        }

        var content = reader.ReadElementContentAsString();
        Console.Error.WriteLine($"[ReadVerticesElementBytes] Content length={content?.Length ?? 0}");
        
        var (bytes, skipDecryption) = TryReadVerticesAsPlainFloatOrBase64(content, vertexCount);
        Console.Error.WriteLine($"[ReadVerticesElementBytes] Result: bytesLen={bytes.Length}, skipDecryption={skipDecryption}");
        
        return (bytes, skipDecryption);
    }

    private static byte[] ReadFacetsElementBytes(XmlReader reader, int faceCount)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return Array.Empty<byte>();
        }

        var content = reader.ReadElementContentAsString();
        return TryReadFacetsAsPlainIntOrBase64(content, faceCount);
    }

    private static bool TryPopulateGeometryFromDocument(
        string filePath,
        DcmMetadata metadata,
        CoordinateDecodingMode mode,
        List<Point3D> positions,
        List<int> triangleIndices)
    {
        var document = XDocument.Load(filePath, LoadOptions.None);
        List<Point3D>? pendingVertices = null;
        PendingFacets? pendingFacets = null;
        var foundGeometry = false;

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName.Equals("Vertices", StringComparison.OrdinalIgnoreCase))
            {
                var vertexCount = ReadOptionalIntAttribute(element, "vertex_count") ?? ReadOptionalIntAttribute(element, "Count") ?? 0;
                var checkValue = ReadOptionalUIntAttribute(element, "check_value");
                var (bytes, skipDecryption) = TryReadVerticesAsPlainFloatOrBase64(element.Value, vertexCount);
                var parentName = element.Parent?.Name?.LocalName ?? string.Empty;
                var effectiveSchema = parentName.Equals("CE", StringComparison.OrdinalIgnoreCase) ? "CE" : metadata.Schema;
                try
                {
                    pendingVertices = DecodeVertices(bytes, vertexCount, checkValue, effectiveSchema, metadata.Properties, mode, skipDecryption);
                }
                catch (InvalidDataException)
                {
                    pendingVertices = null;
                }

                if (pendingVertices is { Count: > 0 } && pendingFacets is not null)
                {
                    var pendingFaces = DecodeFacets(pendingFacets.Value.Bytes, pendingFacets.Value.FaceCount, pendingVertices.Count, pendingVertices);
                    AppendIndexedMesh(positions, triangleIndices, pendingVertices, pendingFaces);
                    pendingVertices = null;
                    pendingFacets = null;
                    foundGeometry = true;
                }

                continue;
            }

            if (!element.Name.LocalName.Equals("Facets", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var faceCount = ReadOptionalIntAttribute(element, "facet_count") ?? ReadOptionalIntAttribute(element, "Count") ?? 0;
            var facetBytes = TryReadFacetsAsPlainIntOrBase64(element.Value, faceCount);
            pendingFacets = new PendingFacets(facetBytes, faceCount);

            if (pendingVertices is { Count: > 0 })
            {
                var pendingFaces = DecodeFacets(pendingFacets.Value.Bytes, pendingFacets.Value.FaceCount, pendingVertices.Count, pendingVertices);
                AppendIndexedMesh(positions, triangleIndices, pendingVertices, pendingFaces);
                pendingVertices = null;
                pendingFacets = null;
                foundGeometry = true;
            }
        }

        if (!foundGeometry && triangleIndices.Count == 0 && pendingVertices is { Count: > 0 })
        {
            AppendSequentialTriangles(positions, triangleIndices, pendingVertices);
            foundGeometry = positions.Count > 0 && triangleIndices.Count > 0;
        }

        return foundGeometry;
    }

    private static bool TryPopulateGeometryFromEmbeddedHps(
        string filePath,
        CoordinateDecodingMode mode,
        List<Point3D> positions,
        List<int> triangleIndices,
        IReadOnlyDictionary<string, string>? outerFileProperties = null)
    {
        var root = XDocument.Load(filePath, LoadOptions.None);
        var queue = new Queue<XDocument>();
        var visitedPayloads = new HashSet<string>(StringComparer.Ordinal);
        var embeddedCandidates = new List<XDocument>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var document = queue.Dequeue();
            foreach (var payload in ExtractEmbeddedHpsPayloads(document))
            {
                if (!visitedPayloads.Add(payload))
                {
                    continue;
                }

                try
                {
                    var embeddedDocument = XDocument.Parse(payload, LoadOptions.None);
                    embeddedCandidates.Add(embeddedDocument);
                    queue.Enqueue(embeddedDocument);
                }
                catch (XmlException)
                {
                    // Ignore malformed embedded payloads and continue scanning others.
                }
                catch (InvalidDataException)
                {
                    // Ignore undecodable embedded payloads and continue scanning others.
                }
            }
        }

        var foundGeometry = false;

        foreach (var candidate in embeddedCandidates
                     .OrderByDescending(ScoreEmbeddedHpsCandidate)
                     .ThenByDescending(candidate => candidate.Descendants().Count()))
        {
            var candidatePositions = new List<Point3D>();
            var candidateTriangles = new List<int>();
            var embeddedMetadata = ReadMetadata(candidate);
            if (outerFileProperties is { Count: > 0 })
            {
                // Supplement the inner HPS metadata with the outer file's properties so that
                // PackageLockList (and other outer-only keys) are available for CE decryption.
                // Inner HPS properties take precedence where they define the same key.
                var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in outerFileProperties)
                {
                    merged[pair.Key] = pair.Value;
                }
                foreach (var pair in embeddedMetadata.Properties)
                {
                    merged[pair.Key] = pair.Value;
                }
                embeddedMetadata = new DcmMetadata(embeddedMetadata.Schema, merged);
            }
            if (!TryPopulateGeometryFromDocument(candidate, embeddedMetadata, mode, candidatePositions, candidateTriangles) ||
                candidatePositions.Count == 0 ||
                candidateTriangles.Count == 0)
            {
                continue;
            }

            AppendDecodedSection(positions, triangleIndices, candidatePositions, candidateTriangles);
            foundGeometry = true;
        }

        return foundGeometry;
    }

    private static void AppendDecodedSection(
        List<Point3D> positions,
        List<int> triangleIndices,
        List<Point3D> candidatePositions,
        List<int> candidateTriangles)
    {
        var baseIndex = positions.Count;
        positions.AddRange(candidatePositions);
        foreach (var triangleIndex in candidateTriangles)
        {
            triangleIndices.Add(baseIndex + triangleIndex);
        }
    }

    private static double ScoreEmbeddedHpsCandidate(XDocument document)
    {
        if (document.Root is null)
        {
            return double.NegativeInfinity;
        }

        var score = 0.0;
        var textureSignals = document.Root
            .DescendantsAndSelf()
            .SelectMany(element => element.Attributes().Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}"))
            .Concat(document.Root.DescendantsAndSelf().Select(element => element.Name.LocalName))
            .Concat(document.Root.DescendantsAndSelf().Select(element => element.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value));

        var textureScore = 0.0;

        foreach (var signal in textureSignals)
        {
            if (signal.IndexOf("TextureImage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                textureScore += 100;
            }
            else if (signal.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                textureScore += 20;
            }

            if (signal.IndexOf("PNG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signal.IndexOf("JPG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signal.IndexOf("JPEG", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                textureScore += 30;
            }
        }

        var maxVertexCount = document
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("Vertices", StringComparison.OrdinalIgnoreCase))
            .Select(element => ReadOptionalIntAttribute(element, "vertex_count") ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        var maxFacetCount = document
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("Facets", StringComparison.OrdinalIgnoreCase))
            .Select(element => ReadOptionalIntAttribute(element, "facet_count") ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        // Primary signal: actual mesh size.
        score += maxVertexCount * 10.0;
        score += maxFacetCount * 5.0;

        // Secondary signal: texture metadata is useful but must not override geometry size.
        score += Math.Min(textureScore, 500.0);

        return score;
    }

    private static IEnumerable<string> ExtractEmbeddedHpsPayloads(XDocument document)
    {
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (TryExtractHpsPayload(attribute.Value, out var payload))
            {
                yield return payload;
            }
        }

        foreach (var element in document.Descendants())
        {
            if (TryExtractHpsPayload(element.Value, out var payload))
            {
                yield return payload;
            }
        }
    }

    private static bool TryExtractHpsPayload(string? text, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var start = trimmed.IndexOf("<HPS", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return false;
        }

        var end = trimmed.LastIndexOf("</HPS>", StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            return false;
        }

        var endExclusive = end + "</HPS>".Length;
        if (endExclusive <= start || endExclusive > trimmed.Length)
        {
            return false;
        }

        payload = trimmed.Substring(start, endExclusive - start).Trim();
        return payload.StartsWith("<HPS", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryPopulateGeometryFromDocument(
        XDocument document,
        DcmMetadata metadata,
        CoordinateDecodingMode mode,
        List<Point3D> positions,
        List<int> triangleIndices)
    {
        List<Point3D>? pendingVertices = null;
        PendingFacets? pendingFacets = null;
        var foundGeometry = false;

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName.Equals("Vertices", StringComparison.OrdinalIgnoreCase))
            {
                var vertexCount = ReadOptionalIntAttribute(element, "vertex_count") ?? ReadOptionalIntAttribute(element, "Count") ?? 0;
                var checkValue = ReadOptionalUIntAttribute(element, "check_value");
                var (bytes, skipDecryption) = TryReadVerticesAsPlainFloatOrBase64(element.Value, vertexCount);
                var parentName = element.Parent?.Name?.LocalName ?? string.Empty;
                var effectiveSchema = parentName.Equals("CE", StringComparison.OrdinalIgnoreCase) ? "CE" : metadata.Schema;
                try
                {
                    pendingVertices = DecodeVertices(bytes, vertexCount, checkValue, effectiveSchema, metadata.Properties, mode, skipDecryption);
                }
                catch (InvalidDataException)
                {
                    pendingVertices = null;
                }

                if (pendingVertices is { Count: > 0 } && pendingFacets is not null)
                {
                    var pendingFaces = DecodeFacets(pendingFacets.Value.Bytes, pendingFacets.Value.FaceCount, pendingVertices.Count, pendingVertices);
                    AppendIndexedMesh(positions, triangleIndices, pendingVertices, pendingFaces);
                    pendingVertices = null;
                    pendingFacets = null;
                    foundGeometry = true;
                }

                continue;
            }

            if (!element.Name.LocalName.Equals("Facets", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var faceCount = ReadOptionalIntAttribute(element, "facet_count") ?? ReadOptionalIntAttribute(element, "Count") ?? 0;
            var facetBytes = TryReadFacetsAsPlainIntOrBase64(element.Value, faceCount);
            pendingFacets = new PendingFacets(facetBytes, faceCount);

            if (pendingVertices is { Count: > 0 })
            {
                var pendingFaces = DecodeFacets(pendingFacets.Value.Bytes, pendingFacets.Value.FaceCount, pendingVertices.Count, pendingVertices);
                AppendIndexedMesh(positions, triangleIndices, pendingVertices, pendingFaces);
                pendingVertices = null;
                pendingFacets = null;
                foundGeometry = true;
            }
        }

        if (!foundGeometry && triangleIndices.Count == 0 && pendingVertices is { Count: > 0 })
        {
            AppendSequentialTriangles(positions, triangleIndices, pendingVertices);
            foundGeometry = positions.Count > 0 && triangleIndices.Count > 0;
        }

        return foundGeometry;
    }

    private static DcmMetadata ReadMetadata(XDocument document)
    {
        var schema = document.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("Schema", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.Descendants().Where(element => element.Name.LocalName.Equals("Property", StringComparison.OrdinalIgnoreCase)))
        {
            var name = property.Attribute("name")?.Value;
            var value = property.Attribute("value")?.Value;
            if (string.IsNullOrWhiteSpace(name) || value is null)
            {
                continue;
            }

            properties[name] = value;
        }

        return new DcmMetadata(schema, properties);
    }

    private static (byte[] Bytes, bool SkipCeDecryption) TryReadVerticesAsPlainFloatOrBase64(string content, int vertexCount)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (Array.Empty<byte>(), false);
        }

        // Try base64 first (for encrypted/binary formats)
        try
        {
            var bytes = Convert.FromBase64String(content.Trim());
            if (bytes.Length > 0)
            {
                // Base64 might be encrypted, so allow CE decryption
                return (bytes, false);
            }
        }
        catch (FormatException)
        {
            // Not base64, try plain text floats
        }

        // Try to parse as plain text floats (3 per vertex)
        var plainTextBytes = TryParseVerticesAsPlainFloats(content, vertexCount);
        // If we successfully parsed as plain text, skip CE decryption (they're already decoded)
        return (plainTextBytes, plainTextBytes.Length > 0);
    }

    private static byte[] TryParseVerticesAsPlainFloats(string content, int vertexCount)
    {
        try
        {
            var tokens = content.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < vertexCount * 3)
            {
                return Array.Empty<byte>();
            }

            var bytes = new byte[vertexCount * 12]; // 3 floats per vertex, 4 bytes each
            var offset = 0;

            for (var i = 0; i < vertexCount; i++)
            {
                if (i * 3 + 2 >= tokens.Length)
                {
                    break;
                }

                if (!float.TryParse(tokens[i * 3], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                    !float.TryParse(tokens[i * 3 + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                    !float.TryParse(tokens[i * 3 + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    return Array.Empty<byte>();
                }

                Buffer.BlockCopy(BitConverter.GetBytes(x), 0, bytes, offset, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(y), 0, bytes, offset + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(z), 0, bytes, offset + 8, 4);
                offset += 12;
            }

            return bytes;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static byte[] TryReadFacetsAsPlainIntOrBase64(string content, int faceCount)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<byte>();
        }

        // Try base64 first (for encrypted/binary formats)
        try
        {
            var bytes = Convert.FromBase64String(content.Trim());
            if (bytes.Length > 0)
            {
                return bytes;
            }
        }
        catch (FormatException)
        {
            // Not base64, try plain text ints
        }

        // Try to parse as plain text integers (3 per facet)
        return TryParseFacetsAsPlainInts(content, faceCount);
    }

    private static byte[] TryParseFacetsAsPlainInts(string content, int faceCount)
    {
        if (string.IsNullOrWhiteSpace(content) || faceCount <= 0)
        {
            return Array.Empty<byte>();
        }

        try
        {
            var tokens = content.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < faceCount * 3)
            {
                return Array.Empty<byte>();
            }

            var bytes = new byte[faceCount * 12]; // 3 ints per facet, 4 bytes each
            var offset = 0;

            for (var i = 0; i < faceCount; i++)
            {
                if (i * 3 + 2 >= tokens.Length)
                {
                    break;
                }

                if (!int.TryParse(tokens[i * 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v0) ||
                    !int.TryParse(tokens[i * 3 + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v1) ||
                    !int.TryParse(tokens[i * 3 + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v2))
                {
                    return Array.Empty<byte>();
                }

                Buffer.BlockCopy(BitConverter.GetBytes(v0), 0, bytes, offset, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(v1), 0, bytes, offset + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(v2), 0, bytes, offset + 8, 4);
                offset += 12;
            }

            return bytes;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static byte[] ReadBase64ElementBytes(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<byte>();
        }

        return Convert.FromBase64String(content.Trim());
    }

    private static int ReadRequiredIntAttribute(XmlReader reader, string attributeName)
    {
        var value = reader.GetAttribute(attributeName);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Missing or invalid '{attributeName}' attribute on <{reader.Name}>.");
        }

        return result;
    }

    private static int? ReadOptionalIntAttribute(XmlReader reader, string attributeName)
    {
        var value = reader.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static int ReadRequiredIntAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Missing or invalid '{attributeName}' attribute on <{element.Name.LocalName}>.");
        }

        return result;
    }

    private static int? ReadOptionalIntAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static uint? ReadOptionalUIntAttribute(XmlReader reader, string attributeName)
    {
        var value = reader.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static uint? ReadOptionalUIntAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static List<Point3D> DecodeVertices(
        byte[] encodedBytes,
        int vertexCount,
        uint? checkValue,
        string schema,
        IReadOnlyDictionary<string, string> properties,
        CoordinateDecodingMode mode,
        bool skipCeDecryption = false)
    {
        Console.Error.WriteLine($"[DecodeVertices] Input: vertexCount={vertexCount}, bytesLen={encodedBytes.Length}, skipDecryption={skipCeDecryption}, schema={schema}");
        
        var expectedSize = vertexCount * PointStrideBytes;
        
        // Try CE-style decryption for schema="CE" or files with PackageLockList encryption
        // UNLESS the bytes came from plain text parsing (skipCeDecryption = true)
        var isEncryptedFile = !skipCeDecryption && (schema.Equals("CE", StringComparison.OrdinalIgnoreCase) || properties.ContainsKey("PackageLockList"));
        Console.Error.WriteLine($"[DecodeVertices] isEncryptedFile={isEncryptedFile}, expectedSize={expectedSize}");
        
        var rawBytes = isEncryptedFile
            ? DecryptCeVertices(encodedBytes, properties, expectedSize, checkValue)
            : encodedBytes;

        Console.Error.WriteLine($"[DecodeVertices] After decrypt: rawBytesLen={rawBytes.Length}");

        var decodedPoints = mode switch
        {
            CoordinateDecodingMode.Absolute => BuildTrianglePoints(rawBytes, useDeltaDecoding: false),
            CoordinateDecodingMode.Delta => BuildTrianglePoints(rawBytes, useDeltaDecoding: true),
            _ => SelectBestPointSet(
                BuildTrianglePoints(rawBytes, useDeltaDecoding: false),
                BuildTrianglePoints(rawBytes, useDeltaDecoding: true))
        };

        var enableInt32Fallback = string.Equals(
            Environment.GetEnvironmentVariable("DCMVIEWER_ENABLE_INT32_VERTEX_FALLBACK"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        if (enableInt32Fallback)
        {
            var int32DecodedPoints = DecodeInt32PointCandidates(rawBytes, vertexCount);
            if (ScorePointSet(int32DecodedPoints, vertexCount) > ScorePointSet(decodedPoints, vertexCount) + 0.5)
            {
                Console.Error.WriteLine("[DecodeVertices] Using int32-scaled fallback candidate");
                decodedPoints = int32DecodedPoints;
            }
        }

        Console.Error.WriteLine($"[DecodeVertices] Decoded points: count={decodedPoints.Count}, plausible={ArePointsPlausible(decodedPoints)}");

        var checksumMatches = !checkValue.HasValue || MatchesCheckValue(rawBytes, checkValue.Value);
        var pointsPlausible = ArePointsPlausible(decodedPoints);
        var coverage = (double)decodedPoints.Count / Math.Max(1, vertexCount);

        var maxAbsCoordinate = ComputeMaxAbs(decodedPoints);

        var allowLenientHighCoverage = enableInt32Fallback;
        var hasLockedMarkers =
            properties.ContainsKey("EKID") ||
            properties.ContainsKey("PackageLockList") ||
            properties.ContainsKey("IntegrityCheck");
        var allowLockedHighCoverage =
            hasLockedMarkers &&
            coverage >= 0.97 &&
            maxAbsCoordinate <= 50000.0;
        var allowCoverageBypass =
            (allowLenientHighCoverage &&
             coverage >= 0.95 &&
             maxAbsCoordinate <= 2500.0) ||
            allowLockedHighCoverage;

        if (!checksumMatches && !pointsPlausible && !allowCoverageBypass)
        {
            Console.Error.WriteLine($"[DecodeVertices] Throwing InvalidDataException: checkValue mismatch and implausible points");
            throw new InvalidDataException("Vertex data checksum verification failed.");
        }

        if (!checksumMatches && !pointsPlausible && allowCoverageBypass)
        {
            Console.Error.WriteLine(
                $"[DecodeVertices] Accepting high-coverage fallback despite checksum/plausibility mismatch (coverage={coverage:F3}, maxAbs={maxAbsCoordinate:F3}, locked={hasLockedMarkers})");
        }

        return decodedPoints;
    }

    private static byte[] DecryptCeVertices(
        byte[] encodedBytes,
        IReadOnlyDictionary<string, string> properties,
        int expectedSize,
        uint? checkValue)
    {
        var exhaustiveCeSearch = string.Equals(
            Environment.GetEnvironmentVariable("DCMVIEWER_CE_EXHAUSTIVE"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        byte[]? bestFallback = null;
        var bestFallbackScore = double.NegativeInfinity;

        void ConsiderFallback(byte[] candidate)
        {
            var score = ScoreVertexCandidate(candidate, expectedSize / PointStrideBytes);
            if (score > bestFallbackScore)
            {
                bestFallbackScore = score;
                bestFallback = candidate;
            }
        }

        // Some files are not CE-encrypted despite package-lock markers.
        // Keep raw and per-word-swapped buffers in the fallback competition.
        foreach (var rawCandidate in BuildTrimCandidates(encodedBytes, expectedSize))
        {
            if (checkValue.HasValue && MatchesCheckValue(rawCandidate, checkValue.Value))
            {
                return rawCandidate;
            }

            ConsiderFallback(rawCandidate);
        }

        var swappedEncodedBytes = (byte[])encodedBytes.Clone();
        SwapEndiannessPerWordInBlocks(swappedEncodedBytes);
        foreach (var rawSwappedCandidate in BuildTrimCandidates(swappedEncodedBytes, expectedSize))
        {
            if (checkValue.HasValue && MatchesCheckValue(rawSwappedCandidate, checkValue.Value))
            {
                return rawSwappedCandidate;
            }

            ConsiderFallback(rawSwappedCandidate);
        }

        // Allow explicit subscription passwords via env var (semicolon-separated).
        // These are tried FIRST as direct Latin-1 Blowfish keys (PreSwap:true, PostSwap:false).
        Console.Error.WriteLine($"[DecryptCeVertices] Checking EKID in properties: {string.Join(", ", properties.Keys)}");
        if (properties.TryGetValue("EKID", out var ekidVal))
        {
            Console.Error.WriteLine($"[DecryptCeVertices] Found EKID: {ekidVal}");
        }
        if (properties.TryGetValue("EKID", out var ekidVal2) && ekidVal2 == "1")
        {
            var extraPwds = Environment.GetEnvironmentVariable("DCMVIEWER_SUBSCRIPTION_PASSWORDS");
            Console.Error.WriteLine($"[DecryptCeVertices] EKID=1 detected. Subscription passwords from env: {extraPwds}");
            if (!string.IsNullOrEmpty(extraPwds))
            {
                foreach (var pwd in extraPwds.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var pwdTrimmed = pwd.Trim();
                    Console.Error.WriteLine($"[DecryptCeVertices] Trying subscription password: '{pwdTrimmed}'");
                    var pwdKey = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(pwdTrimmed);
                    var decrypted = DecryptCeBuffer(encodedBytes, pwdKey, expectedSize, preSwap: true, postSwap: false);
                    if (checkValue.HasValue)
                    {
                        var adler = MatchesCheckValue(decrypted, checkValue.Value);
                        Console.Error.WriteLine($"[DecryptCeVertices] Password '{pwdTrimmed}' checksum: {adler}");
                        if (adler)
                        {
                            Console.Error.WriteLine($"[DecryptCeVertices] Subscription password matched: '{pwdTrimmed}'");
                            return decrypted;
                        }
                    } else {
                        Console.Error.WriteLine($"[DecryptCeVertices] Password '{pwdTrimmed}' (no checksum to verify)");
                    }
                    ConsiderFallback(decrypted);
                }
            }
        }

        var keyCandidates = exhaustiveCeSearch
            ? BuildCeKeyVariants(properties, exhaustive: true)
                .Concat(BuildCeKeyVariants(RemovePackageLock(properties), exhaustive: true))
                .ToArray()
            : BuildCeKeyVariants(properties, exhaustive: false).ToArray();

        // (PreSwap: true, PostSwap: false) is the mode used by Open3SDCM and matches 3Shape's
        // reference encryption: swap each 32-bit word before ECB decrypt, use the output directly.
        var swapModes = exhaustiveCeSearch
            ? new[]
            {
                (PreSwap: true, PostSwap: false),
                (PreSwap: true, PostSwap: true),
                (PreSwap: false, PostSwap: false),
                (PreSwap: false, PostSwap: true)
            }
            : new[]
            {
                (PreSwap: true, PostSwap: false),
                (PreSwap: true, PostSwap: true),
                (PreSwap: false, PostSwap: false)
            };

        foreach (var keyCandidate in keyCandidates)
        {
            foreach (var swapMode in swapModes)
            {
                var decrypted = DecryptCeBuffer(
                    encodedBytes,
                    keyCandidate,
                    expectedSize,
                    swapMode.PreSwap,
                    swapMode.PostSwap);

                ConsiderFallback(decrypted);

                if (!checkValue.HasValue || MatchesCheckValue(decrypted, checkValue.Value))
                {
                    return decrypted;
                }
            }
        }

        return bestFallback ?? Array.Empty<byte>();
    }

    private static bool ArePointsPlausible(List<Point3D> points)
    {
        if (points.Count < 3)
        {
            return false;
        }

        var bounds = ComputeBounds(points);
        if (bounds == Rect3D.Empty)
        {
            return false;
        }

        var diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
        if (!double.IsFinite(diagonal) || diagonal <= 0 || diagonal > 5000)
        {
            return false;
        }

        var maxAbs = 0.0;
        foreach (var point in points)
        {
            maxAbs = Math.Max(maxAbs, Math.Abs(point.X));
            maxAbs = Math.Max(maxAbs, Math.Abs(point.Y));
            maxAbs = Math.Max(maxAbs, Math.Abs(point.Z));
        }

        return double.IsFinite(maxAbs) && maxAbs < 5000;
    }

    private static double ScoreRawVertexBytes(byte[] rawBytes)
    {
        if (rawBytes.Length < 12)
        {
            return double.NegativeInfinity;
        }

        var sampleBytes = Math.Min(rawBytes.Length - (rawBytes.Length % 4), 4096);
        if (sampleBytes <= 0)
        {
            return double.NegativeInfinity;
        }

        var floats = sampleBytes / 4;
        var finiteCount = 0;
        var saneCount = 0;
        var nearZeroCount = 0;

        for (var offset = 0; offset < sampleBytes; offset += 4)
        {
            var value = BitConverter.ToSingle(rawBytes, offset);
            if (!float.IsFinite(value))
            {
                continue;
            }

            finiteCount++;
            var abs = Math.Abs(value);
            if (abs < 10000f)
            {
                saneCount++;
            }

            if (abs < 1e-7f)
            {
                nearZeroCount++;
            }
        }

        if (finiteCount == 0)
        {
            return double.NegativeInfinity;
        }

        var finiteRatio = (double)finiteCount / floats;
        var saneRatio = (double)saneCount / finiteCount;
        var zeroRatio = (double)nearZeroCount / finiteCount;
        return (finiteRatio * 2.0) + (saneRatio * 4.0) - (zeroRatio * 0.5);
    }

    private static double ScoreVertexCandidate(byte[] rawBytes, int expectedVertexCount)
    {
        if (rawBytes.Length < PointStrideBytes)
        {
            return double.NegativeInfinity;
        }

        var exhaustiveCeSearch = string.Equals(
            Environment.GetEnvironmentVariable("DCMVIEWER_CE_EXHAUSTIVE"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        var absolutePoints = BuildTrianglePoints(rawBytes, useDeltaDecoding: false);
        var deltaPoints = BuildTrianglePoints(rawBytes, useDeltaDecoding: true);

        var absoluteScore = ScorePointSet(absolutePoints, expectedVertexCount);
        var deltaScore = ScorePointSet(deltaPoints, expectedVertexCount);
        var int32Score = exhaustiveCeSearch
            ? ScorePointSet(DecodeInt32PointCandidates(rawBytes, expectedVertexCount), expectedVertexCount)
            : double.NegativeInfinity;
        var pointSetScore = Math.Max(Math.Max(absoluteScore, deltaScore), int32Score);

        return ScoreRawVertexBytes(rawBytes) + pointSetScore;
    }

    private static double ScorePointSet(List<Point3D> points, int expectedVertexCount)
    {
        if (points.Count == 0)
        {
            return double.NegativeInfinity;
        }

        var coverage = (double)points.Count / Math.Max(1, expectedVertexCount);
        var boundedCoverage = Math.Min(1.5, coverage);
        var bounds = ComputeBounds(points);
        var maxAbs = ComputeMaxAbs(points);

        var score = boundedCoverage * 8.0;
        if (ArePointsPlausible(points))
        {
            score += 4.0;
        }

        if (bounds != Rect3D.Empty)
        {
            var diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
            if (double.IsFinite(diagonal))
            {
                // Favor compact dental-scale bounds.
                if (diagonal < 500.0)
                {
                    score += 2.0;
                }
                else if (diagonal > 5000.0)
                {
                    score -= Math.Min(6.0, Math.Log10(Math.Max(1.0, diagonal / 5000.0)) * 4.0);
                }
            }
        }

        if (double.IsFinite(maxAbs))
        {
            if (maxAbs < 500.0)
            {
                score += 2.0;
            }
            else if (maxAbs > 5000.0)
            {
                score -= Math.Min(8.0, Math.Log10(Math.Max(1.0, maxAbs / 5000.0)) * 5.0);
            }
        }

        return score;
    }

    private static Dictionary<string, string> RemovePackageLock(IReadOnlyDictionary<string, string> properties)
    {
        var filtered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in properties)
        {
            if (pair.Key.Equals("PackageLockList", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filtered[pair.Key] = pair.Value;
        }

        return filtered;
    }

    private static bool MatchesCheckValue(byte[] rawBytes, uint checkValue)
    {
        var adler = ComputeAdler32(rawBytes);
        return adler == checkValue || SwapUInt32Endianness(adler) == checkValue;
    }

    private static List<Point3D> BuildTrianglePoints(byte[] bytes, bool useDeltaDecoding)
    {
        var validLength = bytes.Length - (bytes.Length % PointStrideBytes);
        var points = new List<Point3D>(validLength / PointStrideBytes);

        float prevX = 0;
        float prevY = 0;
        float prevZ = 0;

        for (var offset = 0; offset < validLength; offset += PointStrideBytes)
        {
            var x = BitConverter.ToSingle(bytes, offset);
            var y = BitConverter.ToSingle(bytes, offset + 4);
            var z = BitConverter.ToSingle(bytes, offset + 8);

            if (useDeltaDecoding)
            {
                x += prevX;
                y += prevY;
                z += prevZ;
            }

            prevX = x;
            prevY = y;
            prevZ = z;

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
                float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z))
            {
                continue;
            }

            points.Add(new Point3D(x, y, z));
        }

        return points;
    }

    private static List<Point3D> DecodeInt32PointCandidates(byte[] bytes, int expectedVertexCount)
    {
        var scales = new[] { 1d, 10d, 100d, 1000d, 10000d, 100000d, 1000000d, 10000000d, 100000000d };
        List<Point3D>? best = null;
        var bestScore = double.NegativeInfinity;
        var bestScale = 1d;
        var bestUseDelta = false;

        foreach (var scale in scales)
        {
            foreach (var useDelta in new[] { false, true })
            {
                var candidate = BuildTrianglePointsFromInt32(bytes, scale, useDelta);
                var score = ScorePointSet(candidate, expectedVertexCount);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    bestScale = scale;
                    bestUseDelta = useDelta;
                }
            }
        }

        if (expectedVertexCount >= 7000 && best is not null &&
            string.Equals(Environment.GetEnvironmentVariable("DCMVIEWER_LOG_INT32_CANDIDATES"), "1", StringComparison.OrdinalIgnoreCase))
        {
            var bounds = ComputeBounds(best);
            Console.Error.WriteLine(
                $"[DecodeVertices] Int32 candidate selected: scale={bestScale}, delta={bestUseDelta}, points={best.Count}, score={bestScore:F3}, bounds=({bounds.X:F3},{bounds.Y:F3},{bounds.Z:F3}) size=({bounds.SizeX:F3},{bounds.SizeY:F3},{bounds.SizeZ:F3})");
        }

        return best ?? new List<Point3D>();
    }

    private static List<Point3D> BuildTrianglePointsFromInt32(byte[] bytes, double scale, bool useDeltaDecoding)
    {
        if (scale <= 0)
        {
            return new List<Point3D>();
        }

        var validLength = bytes.Length - (bytes.Length % PointStrideBytes);
        var points = new List<Point3D>(validLength / PointStrideBytes);

        double prevX = 0;
        double prevY = 0;
        double prevZ = 0;

        for (var offset = 0; offset < validLength; offset += PointStrideBytes)
        {
            var x = BitConverter.ToInt32(bytes, offset) / scale;
            var y = BitConverter.ToInt32(bytes, offset + 4) / scale;
            var z = BitConverter.ToInt32(bytes, offset + 8) / scale;

            if (useDeltaDecoding)
            {
                x += prevX;
                y += prevY;
                z += prevZ;
            }

            prevX = x;
            prevY = y;
            prevZ = z;

            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            {
                continue;
            }

            points.Add(new Point3D(x, y, z));
        }

        return points;
    }

    private static double ComputeMaxAbs(IReadOnlyList<Point3D> points)
    {
        var maxAbs = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            maxAbs = Math.Max(maxAbs, Math.Abs(point.X));
            maxAbs = Math.Max(maxAbs, Math.Abs(point.Y));
            maxAbs = Math.Max(maxAbs, Math.Abs(point.Z));
        }

        return maxAbs;
    }

    private static List<Triangle> DecodeFacets(
        byte[] rawData,
        int expectedFaceCount,
        int vertexCount,
        IReadOnlyList<Point3D>? vertices = null)
    {
        var paramSets = new (bool use32, int ivp, bool highNib)[]
        {
            (false, 0, false), (false, 1, false), (true, 0, false), (true, 1, false),
            (false, 0, true),  (false, 1, true),  (true, 0, true),  (true, 1, true)
        };
        var candidates = System.Array.ConvertAll(paramSets, p =>
            InterpretFacetsBuffer(rawData, expectedFaceCount, p.use32, p.ivp, p.highNib));

        var bestScore = FacetDecodeScore.Worst;
        var bestGeometryPenalty = double.PositiveInfinity;
        List<Triangle>? best = null;

        for (var ci = 0; ci < candidates.Length; ci++)
        {
            var normalized = NormalizeFacetIndices(candidates[ci], vertexCount);
            var score = ScoreFacetDecode(normalized, expectedFaceCount, vertexCount);
            var geometryPenalty = vertices is { Count: > 0 }
                ? ScoreFacetGeometryPenalty(normalized, vertices)
                : 0.0;

            if (score.CompareTo(bestScore) < 0 ||
                (score.CompareTo(bestScore) == 0 && geometryPenalty < bestGeometryPenalty))
            {
                bestScore = score;
                bestGeometryPenalty = geometryPenalty;
                best = normalized;
            }
        }

        return best ?? new List<Triangle>();
    }

    private static double ScoreFacetGeometryPenalty(List<Triangle> triangles, IReadOnlyList<Point3D> vertices)
    {
        if (triangles.Count == 0 || vertices.Count == 0)
        {
            return double.PositiveInfinity;
        }

        var bounds = ComputeBounds(vertices);
        var diagonal = bounds == Rect3D.Empty
            ? 0.0
            : Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));

        if (!double.IsFinite(diagonal) || diagonal <= 0)
        {
            return double.PositiveInfinity;
        }

        var sampleCount = Math.Min(triangles.Count, 8000);
        var stride = Math.Max(1, triangles.Count / sampleCount);
        var longEdgeThreshold = diagonal * 0.25;
        var tinyAreaThreshold = diagonal * diagonal * 1e-8;

        var considered = 0;
        var longEdges = 0;
        var tinyAreas = 0;

        for (var i = 0; i < triangles.Count; i += stride)
        {
            var triangle = triangles[i];
            if (triangle.V0 < 0 || triangle.V1 < 0 || triangle.V2 < 0 ||
                triangle.V0 >= vertices.Count || triangle.V1 >= vertices.Count || triangle.V2 >= vertices.Count)
            {
                continue;
            }

            var p0 = vertices[triangle.V0];
            var p1 = vertices[triangle.V1];
            var p2 = vertices[triangle.V2];

            var e01 = (p0 - p1).Length;
            var e12 = (p1 - p2).Length;
            var e20 = (p2 - p0).Length;

            if (e01 > longEdgeThreshold) longEdges++;
            if (e12 > longEdgeThreshold) longEdges++;
            if (e20 > longEdgeThreshold) longEdges++;

            var area2 = Vector3D.CrossProduct(p1 - p0, p2 - p0).LengthSquared;
            if (area2 < tinyAreaThreshold)
            {
                tinyAreas++;
            }

            considered++;
        }

        if (considered == 0)
        {
            return double.PositiveInfinity;
        }

        var longEdgeRatio = (double)longEdges / (considered * 3.0);
        var tinyAreaRatio = (double)tinyAreas / considered;
        return (longEdgeRatio * 1000.0) + (tinyAreaRatio * 100.0);
    }

    private static List<Triangle> InterpretFacetsBuffer(byte[] rawData, int expectedFaceCount, bool use32BitPayload, int initialVertexPointer, bool useHighNibbleOpcodes)
    {
        var triangles = new List<Triangle>(Math.Max(expectedFaceCount, 0));
        var edgeList = new List<Edge>();
        var currentEdgeIndex = 0;
        var globalVertexPointer = initialVertexPointer;
        var offset = 0;

        while (offset < rawData.Length)
        {
            if (expectedFaceCount > 0 &&
                triangles.Count > expectedFaceCount + (expectedFaceCount / 10))
            {
                break;
            }

            var opcode = useHighNibbleOpcodes
                ? ((rawData[offset] >> 4) & 0x0F)
                : (rawData[offset] & 0x0F);
            offset++;

            switch (opcode)
            {
                case 0:
                    ExtendCurrentEdge(ref currentEdgeIndex, edgeList, triangles, globalVertexPointer++);
                    AdvanceEdgePointer(ref currentEdgeIndex, edgeList, 2);
                    break;
                case 1:
                    HandlePrevious(ref currentEdgeIndex, edgeList, triangles);
                    break;
                case 2:
                    HandleNext(ref currentEdgeIndex, edgeList, triangles);
                    break;
                case 3:
                    AdvanceEdgePointer(ref currentEdgeIndex, edgeList, 1);
                    break;
                case 4:
                    CreateRestartFace(ref currentEdgeIndex, edgeList, triangles, globalVertexPointer++, globalVertexPointer++, globalVertexPointer++);
                    break;
                case 5:
                {
                    var width = use32BitPayload ? 4 : 2;
                    if (!RequireBytes(rawData, offset, width * 3))
                    {
                        offset = rawData.Length;
                        break;
                    }

                    var v0 = ReadIndex(rawData, ref offset, use32BitPayload);
                    var v1 = ReadIndex(rawData, ref offset, use32BitPayload);
                    var v2 = ReadIndex(rawData, ref offset, use32BitPayload);
                    CreateRestartFace(ref currentEdgeIndex, edgeList, triangles, v0, v1, v2);
                    break;
                }
                case 6:
                {
                    if (!RequireBytes(rawData, offset, 12))
                    {
                        offset = rawData.Length;
                        break;
                    }

                    var v0 = ReadUInt32(rawData, ref offset);
                    var v1 = ReadUInt32(rawData, ref offset);
                    var v2 = ReadUInt32(rawData, ref offset);
                    CreateRestartFace(ref currentEdgeIndex, edgeList, triangles, v0, v1, v2);
                    break;
                }
                case 7:
                {
                    var width = use32BitPayload ? 4 : 2;
                    if (!RequireBytes(rawData, offset, width))
                    {
                        offset = rawData.Length;
                        break;
                    }

                    ExtendCurrentEdge(ref currentEdgeIndex, edgeList, triangles, ReadIndex(rawData, ref offset, use32BitPayload));
                    AdvanceEdgePointer(ref currentEdgeIndex, edgeList, 2);
                    break;
                }
                case 8:
                {
                    if (!RequireBytes(rawData, offset, 4))
                    {
                        offset = rawData.Length;
                        break;
                    }

                    ExtendCurrentEdge(ref currentEdgeIndex, edgeList, triangles, ReadUInt32(rawData, ref offset));
                    AdvanceEdgePointer(ref currentEdgeIndex, edgeList, 2);
                    break;
                }
                case 9:
                    RemoveCurrentEdge(ref currentEdgeIndex, edgeList);
                    break;
                case 10:
                    globalVertexPointer++;
                    break;
            }
        }

        return triangles;
    }

    private static List<Triangle> NormalizeFacetIndices(List<Triangle> triangles, int vertexCount)
    {
        if (triangles.Count == 0 || vertexCount <= 0)
        {
            return triangles;
        }

        var minIndex = int.MaxValue;
        var maxIndex = int.MinValue;
        foreach (var triangle in triangles)
        {
            minIndex = Math.Min(minIndex, Math.Min(triangle.V0, Math.Min(triangle.V1, triangle.V2)));
            maxIndex = Math.Max(maxIndex, Math.Max(triangle.V0, Math.Max(triangle.V1, triangle.V2)));
        }

        var shifts = new HashSet<int>
        {
            0,
            -minIndex,
            (vertexCount - 1) - maxIndex,
            -1,
            1,
            -2,
            2
        };

        var bestShift = 0;
        var bestValidTriangles = CountValidTriangles(triangles, vertexCount, bestShift);

        foreach (var shift in shifts)
        {
            var validTriangles = CountValidTriangles(triangles, vertexCount, shift);
            if (validTriangles > bestValidTriangles)
            {
                bestValidTriangles = validTriangles;
                bestShift = shift;
            }
        }

        if (bestShift == 0)
        {
            return triangles;
        }

        var shifted = new List<Triangle>(triangles.Count);
        foreach (var triangle in triangles)
        {
            shifted.Add(new Triangle(triangle.V0 + bestShift, triangle.V1 + bestShift, triangle.V2 + bestShift));
        }

        return shifted;
    }

    private static int CountValidTriangles(List<Triangle> triangles, int vertexCount, int shift)
    {
        var validTriangles = 0;
        foreach (var triangle in triangles)
        {
            var v0 = triangle.V0 + shift;
            var v1 = triangle.V1 + shift;
            var v2 = triangle.V2 + shift;
            if (v0 < 0 || v1 < 0 || v2 < 0 || v0 >= vertexCount || v1 >= vertexCount || v2 >= vertexCount)
            {
                continue;
            }

            validTriangles++;
        }

        return validTriangles;
    }

    private static FacetDecodeScore ScoreFacetDecode(List<Triangle> triangles, int expectedFaceCount, int vertexCount)
    {
        if (triangles.Count == 0)
        {
            return FacetDecodeScore.Worst;
        }

        var outOfRangeCount = 0;
        var usage = new Dictionary<int, int>();

        foreach (var triangle in triangles)
        {
            CountVertex(triangle.V0, usage, vertexCount, ref outOfRangeCount);
            CountVertex(triangle.V1, usage, vertexCount, ref outOfRangeCount);
            CountVertex(triangle.V2, usage, vertexCount, ref outOfRangeCount);
        }

        var maxUsage = usage.Count == 0 ? 0 : usage.Values.Max();
        var expectedDelta = Math.Abs(triangles.Count - expectedFaceCount);
        var outOfRangeRatio = (double)outOfRangeCount / Math.Max(1, triangles.Count * 3);
        var maxFanRatio = (double)maxUsage / Math.Max(1, triangles.Count);

        return new FacetDecodeScore(expectedDelta, outOfRangeRatio, maxFanRatio);
    }

    private static void CountVertex(int vertex, Dictionary<int, int> usage, int vertexCount, ref int outOfRangeCount)
    {
        if (vertex < 0 || vertex >= vertexCount)
        {
            outOfRangeCount++;
            return;
        }

        usage.TryGetValue(vertex, out var count);
        usage[vertex] = count + 1;
    }

    private static bool RequireBytes(byte[] rawData, int offset, int count) => offset + count <= rawData.Length;

    private static int ReadIndex(byte[] rawData, ref int offset, bool use32BitPayload)
        => use32BitPayload ? ReadUInt32(rawData, ref offset) : ReadUInt16(rawData, ref offset);

    private static int ReadUInt16(byte[] rawData, ref int offset)
    {
        var value = BitConverter.ToUInt16(rawData, offset);
        offset += sizeof(ushort);
        return value;
    }

    private static int ReadUInt32(byte[] rawData, ref int offset)
    {
        var value = BitConverter.ToUInt32(rawData, offset);
        offset += sizeof(uint);
        return value > int.MaxValue ? -1 : (int)value;
    }

    private static void AdvanceEdgePointer(ref int currentEdgeIndex, List<Edge> edgeList, int step)
    {
        if (edgeList.Count == 0)
        {
            currentEdgeIndex = 0;
            return;
        }

        currentEdgeIndex = (currentEdgeIndex + step) % edgeList.Count;
    }

    private static void CreateRestartFace(ref int currentEdgeIndex, List<Edge> edgeList, List<Triangle> triangles, int v0, int v1, int v2)
    {
        triangles.Add(new Triangle(v0, v1, v2));
        edgeList.Clear();
        edgeList.Add(new Edge(v0, v1));
        edgeList.Add(new Edge(v1, v2));
        edgeList.Add(new Edge(v2, v0));
        currentEdgeIndex = 0;
    }

    private static void ExtendCurrentEdge(ref int currentEdgeIndex, List<Edge> edgeList, List<Triangle> triangles, int vertex)
    {
        if (edgeList.Count == 0)
        {
            return;
        }

        var current = edgeList[currentEdgeIndex];
        triangles.Add(new Triangle(vertex, current.End, current.Start));
        edgeList.RemoveAt(currentEdgeIndex);
        edgeList.Insert(currentEdgeIndex, new Edge(vertex, current.End));
        edgeList.Insert(currentEdgeIndex, new Edge(current.Start, vertex));
    }

    private static void HandlePrevious(ref int currentEdgeIndex, List<Edge> edgeList, List<Triangle> triangles)
    {
        if (edgeList.Count < 2)
        {
            return;
        }

        var previousIndex = (currentEdgeIndex + edgeList.Count - 1) % edgeList.Count;
        var previous = edgeList[previousIndex];
        var current = edgeList[currentEdgeIndex];
        triangles.Add(new Triangle(current.Start, previous.Start, current.End));

        var newEdge = new Edge(previous.Start, current.End);
        var high = Math.Max(currentEdgeIndex, previousIndex);
        var low = Math.Min(currentEdgeIndex, previousIndex);
        edgeList.RemoveAt(high);
        edgeList.RemoveAt(low);
        edgeList.Insert(low, newEdge);
        currentEdgeIndex = edgeList.Count == 0 ? 0 : (low + 1) % edgeList.Count;
    }

    private static void HandleNext(ref int currentEdgeIndex, List<Edge> edgeList, List<Triangle> triangles)
    {
        if (edgeList.Count < 2)
        {
            return;
        }

        var nextIndex = (currentEdgeIndex + 1) % edgeList.Count;
        var current = edgeList[currentEdgeIndex];
        var next = edgeList[nextIndex];
        triangles.Add(new Triangle(current.Start, next.End, current.End));

        var newEdge = new Edge(current.Start, next.End);
        var high = Math.Max(currentEdgeIndex, nextIndex);
        var low = Math.Min(currentEdgeIndex, nextIndex);
        edgeList.RemoveAt(high);
        edgeList.RemoveAt(low);
        edgeList.Insert(low, newEdge);
        currentEdgeIndex = edgeList.Count == 0 ? 0 : (low + 1) % edgeList.Count;
    }

    private static void RemoveCurrentEdge(ref int currentEdgeIndex, List<Edge> edgeList)
    {
        if (edgeList.Count == 0)
        {
            return;
        }

        var previousIndex = (currentEdgeIndex + edgeList.Count - 1) % edgeList.Count;
        var previous = edgeList[previousIndex];
        var current = edgeList[currentEdgeIndex];

        if (previous.Start == current.End && edgeList.Count > 2)
        {
            var high = Math.Max(currentEdgeIndex, previousIndex);
            var low = Math.Min(currentEdgeIndex, previousIndex);
            edgeList.RemoveAt(high);
            edgeList.RemoveAt(low);

            if (edgeList.Count == 0)
            {
                currentEdgeIndex = 0;
                return;
            }

            var newPreviousIndex = (low + edgeList.Count - 1) % edgeList.Count;
            var newCurrentIndex = low % edgeList.Count;
            edgeList[newPreviousIndex] = edgeList[newPreviousIndex] with { End = edgeList[newCurrentIndex].Start };
            currentEdgeIndex = newCurrentIndex;
            return;
        }

        edgeList[previousIndex] = edgeList[previousIndex] with { End = current.End };
        edgeList.RemoveAt(currentEdgeIndex);
        currentEdgeIndex = edgeList.Count == 0 ? 0 : currentEdgeIndex % edgeList.Count;
    }

    private static void AppendIndexedMesh(List<Point3D> positions, List<int> triangleIndices, List<Point3D> partVertices, List<Triangle> faces)
    {
        var baseIndex = positions.Count;
        positions.AddRange(partVertices);

        foreach (var face in faces)
        {
            if (face.V0 < 0 || face.V1 < 0 || face.V2 < 0 ||
                face.V0 >= partVertices.Count || face.V1 >= partVertices.Count || face.V2 >= partVertices.Count)
            {
                continue;
            }

            triangleIndices.Add(baseIndex + face.V0);
            triangleIndices.Add(baseIndex + face.V1);
            triangleIndices.Add(baseIndex + face.V2);
        }
    }

    private static void AppendSequentialTriangles(List<Point3D> positions, List<int> triangleIndices, List<Point3D> vertices)
    {
        var baseIndex = positions.Count;
        positions.AddRange(vertices);

        var triangleVertexCount = vertices.Count - (vertices.Count % 3);
        for (var i = 0; i < triangleVertexCount; i += 3)
        {
            triangleIndices.Add(baseIndex + i);
            triangleIndices.Add(baseIndex + i + 1);
            triangleIndices.Add(baseIndex + i + 2);
        }
    }

    private static void PruneBridgeTriangles(List<Point3D> positions, List<int> triangleIndices)
    {
        if (triangleIndices.Count < 9 || positions.Count == 0)
        {
            return;
        }

        var triangleCount = triangleIndices.Count / 3;
        if (triangleCount < 10_000)
        {
            // Small/compact meshes are often legitimate closed components (e.g. abutments), so skip open-sheet cleanup.
            return;
        }

        var maxEdgeLengths = new double[triangleCount];

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            var i0 = triangleIndices[(triangleIndex * 3) + 0];
            var i1 = triangleIndices[(triangleIndex * 3) + 1];
            var i2 = triangleIndices[(triangleIndex * 3) + 2];

            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Count || i1 >= positions.Count || i2 >= positions.Count)
            {
                maxEdgeLengths[triangleIndex] = double.PositiveInfinity;
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];

            var e01 = (p0 - p1).Length;
            var e12 = (p1 - p2).Length;
            var e20 = (p2 - p0).Length;
            maxEdgeLengths[triangleIndex] = Math.Max(e01, Math.Max(e12, e20));
        }

        var sorted = (double[])maxEdgeLengths.Clone();
        Array.Sort(sorted);
        var median = sorted[sorted.Length / 2];
        if (!double.IsFinite(median) || median <= 0)
        {
            return;
        }

        var bounds = ComputeBounds(positions);
        if (bounds == Rect3D.Empty)
        {
            return;
        }

        var diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
        if (!double.IsFinite(diagonal) || diagonal <= 0)
        {
            return;
        }

        // Keep filtering conservative: only remove triangles with edge spans that are implausibly large for local mesh tessellation.
        var threshold = Math.Min(median * 25.0, diagonal * 0.45);
        if (!double.IsFinite(threshold) || threshold <= 0)
        {
            return;
        }

        var rebuilt = new List<int>(triangleIndices.Count);
        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (maxEdgeLengths[triangleIndex] <= threshold)
            {
                rebuilt.Add(triangleIndices[(triangleIndex * 3) + 0]);
                rebuilt.Add(triangleIndices[(triangleIndex * 3) + 1]);
                rebuilt.Add(triangleIndices[(triangleIndex * 3) + 2]);
            }
        }

        if (rebuilt.Count == 0)
        {
            return;
        }

        var removedTriangleCount = triangleCount - (rebuilt.Count / 3);
        // Ignore tiny/no-op changes so nominally clean files are left untouched.
        if (removedTriangleCount <= 0)
        {
            return;
        }

        triangleIndices.Clear();
        triangleIndices.AddRange(rebuilt);
    }

    private static Rect3D ComputeBounds(List<Point3D> positions)
    {
        if (positions.Count == 0)
        {
            return Rect3D.Empty;
        }

        var minX = positions[0].X;
        var minY = positions[0].Y;
        var minZ = positions[0].Z;
        var maxX = minX;
        var maxY = minY;
        var maxZ = minZ;

        for (var i = 1; i < positions.Count; i++)
        {
            var point = positions[i];
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    private static double ScoreMeshGeometry(int vertexCount, int triangleCount, Rect3D bounds)
    {
        if (vertexCount <= 0 || triangleCount <= 0)
        {
            return double.NegativeInfinity;
        }

        var score = (triangleCount * 2.0) + (vertexCount * 0.25);

        if (bounds == Rect3D.Empty)
        {
            return score - 1000.0;
        }

        var diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
        if (!double.IsFinite(diagonal) || diagonal <= 0)
        {
            return score - 500.0;
        }

        if (diagonal < 5000.0)
        {
            score += 25.0;
        }
        else
        {
            score -= Math.Min(25.0, Math.Log10(Math.Max(1.0, diagonal / 5000.0)) * 10.0);
        }

        return score;
    }

    private static byte[] DecryptCeBuffer(
        byte[] data,
        byte[] key,
        int truncateSize,
        bool preSwap,
        bool postSwap)
    {
        var padded = (byte[])data.Clone();
        if (padded.Length % 8 != 0)
        {
            Array.Resize(ref padded, padded.Length + (8 - (padded.Length % 8)));
        }

        if (preSwap)
        {
            SwapEndiannessPerWordInBlocks(padded);
        }

        var engine = new BlowfishEngine();
        engine.Init(false, new KeyParameter(key));

        var decrypted = new byte[padded.Length];
        for (var offset = 0; offset < padded.Length; offset += engine.GetBlockSize())
        {
            engine.ProcessBlock(padded, offset, decrypted, offset);
        }

        if (postSwap)
        {
            SwapEndiannessPerWordInBlocks(decrypted);
        }

        return TrimToExpectedSize(decrypted, truncateSize);
    }

    private static byte[] TrimToExpectedSize(byte[] data, int expectedSize)
    {
        if (expectedSize > 0 && data.Length > expectedSize)
        {
            var trimmed = new byte[expectedSize];
            Buffer.BlockCopy(data, 0, trimmed, 0, expectedSize);
            return trimmed;
        }

        return data;
    }

    private static IEnumerable<byte[]> BuildTrimCandidates(byte[] data, int expectedSize)
    {
        if (expectedSize <= 0 || data.Length <= expectedSize)
        {
            yield return TrimToExpectedSize(data, expectedSize);
            yield break;
        }

        // Most payloads are either exact-size, have trailing block padding,
        // or carry a 4-byte prefix marker before the encrypted blob.
        yield return CopySlice(data, 0, expectedSize);

        var tailOffset = data.Length - expectedSize;
        if (tailOffset > 0)
        {
            yield return CopySlice(data, tailOffset, expectedSize);
        }
    }

    private static byte[] CopySlice(byte[] data, int offset, int length)
    {
        var slice = new byte[length];
        Buffer.BlockCopy(data, offset, slice, 0, length);
        return slice;
    }

    private static byte[][] BuildCeKeyVariants(IReadOnlyDictionary<string, string> properties, bool exhaustive = true)
    {
        var baseKey = Convert.FromBase64String(BaseCeKeyBase64);

        var results = new List<byte[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var extension in BuildCeKeyExtensions(properties, exhaustive))
        {
            var candidate = new List<byte>(baseKey);
            if (extension.Length > 0)
            {
                candidate.AddRange(extension);
            }

            AddKeyCandidate(results, seen, candidate);

            if (exhaustive && extension.Length > 0)
            {
                // Some files derive session keys by placing metadata material before the base key.
                var prepended = new List<byte>(extension);
                prepended.AddRange(baseKey);
                AddKeyCandidate(results, seen, prepended);

                // Some EKID paths appear to use metadata-only material as the cipher key.
                AddKeyCandidate(results, seen, new List<byte>(extension));

                // Mixed key variant: XOR base key with extension bytes (repeated).
                var mixed = new List<byte>(baseKey.Length + extension.Length);
                for (var i = 0; i < baseKey.Length; i++)
                {
                    mixed.Add((byte)(baseKey[i] ^ extension[i % extension.Length]));
                }

                mixed.AddRange(extension);
                AddKeyCandidate(results, seen, mixed);
            }

            var scrambled = new List<byte>(candidate);
            scrambled.Reverse();
            for (var i = 0; i < scrambled.Count; i++)
            {
                scrambled[i] ^= 0x7B;
            }

            AddKeyCandidate(results, seen, scrambled);
        }

        return results.ToArray();
    }

    private static IEnumerable<byte[]> BuildCeKeyExtensions(IReadOnlyDictionary<string, string> properties, bool exhaustive)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        static IEnumerable<byte[]> YieldDistinct(IEnumerable<byte[]> source, HashSet<string> seenKeys)
        {
            foreach (var item in source)
            {
                var key = Convert.ToHexString(item);
                if (seenKeys.Add(key))
                {
                    yield return item;
                }
            }
        }

        foreach (var item in YieldDistinct(new[] { Array.Empty<byte>() }, seen))
        {
            yield return item;
        }

        var packageExtensions = Array.Empty<byte[]>();
        if (properties.TryGetValue("EKID", out var ekid) && string.Equals(ekid, "1", StringComparison.OrdinalIgnoreCase))
        {
            packageExtensions = BuildPackageLockExtensions(properties).ToArray();
            foreach (var item in YieldDistinct(packageExtensions, seen))
            {
                yield return item;
            }
        }

        var integrityExtensions = BuildIntegrityExtensions(properties).ToArray();
        foreach (var item in YieldDistinct(integrityExtensions, seen))
        {
            yield return item;
        }

        var metadataExtensions = BuildMetadataKeyExtensions(properties, exhaustive).ToArray();
        foreach (var item in YieldDistinct(metadataExtensions, seen))
        {
            yield return item;
        }

        if (!exhaustive)
        {
            yield break;
        }

        foreach (var packageExtension in packageExtensions)
        {
            foreach (var integrityExtension in integrityExtensions)
            {
                if (packageExtension.Length + integrityExtension.Length <= 40)
                {
                    foreach (var item in YieldDistinct(new[] { packageExtension.Concat(integrityExtension).ToArray() }, seen))
                    {
                        yield return item;
                    }
                }

                if (integrityExtension.Length + packageExtension.Length <= 40)
                {
                    foreach (var item in YieldDistinct(new[] { integrityExtension.Concat(packageExtension).ToArray() }, seen))
                    {
                        yield return item;
                    }
                }
            }
        }

        foreach (var metadataExtension in metadataExtensions)
        {
            foreach (var packageExtension in packageExtensions)
            {
                if (metadataExtension.Length + packageExtension.Length <= 40)
                {
                    foreach (var item in YieldDistinct(new[] { metadataExtension.Concat(packageExtension).ToArray() }, seen))
                    {
                        yield return item;
                    }
                }

                if (packageExtension.Length + metadataExtension.Length <= 40)
                {
                    foreach (var item in YieldDistinct(new[] { packageExtension.Concat(metadataExtension).ToArray() }, seen))
                    {
                        yield return item;
                    }
                }
            }

            foreach (var integrityExtension in integrityExtensions)
            {
                if (metadataExtension.Length + integrityExtension.Length <= 40)
                {
                    foreach (var item in YieldDistinct(new[] { metadataExtension.Concat(integrityExtension).ToArray() }, seen))
                    {
                        yield return item;
                    }
                }

                if (integrityExtension.Length + metadataExtension.Length <= 40)
                {
                    foreach (var item in YieldDistinct(new[] { integrityExtension.Concat(metadataExtension).ToArray() }, seen))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    private static IEnumerable<byte[]> BuildPackageLockExtensions(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("PackageLockList", out var value) || string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var items = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        if (items.Length == 0)
        {
            yield break;
        }

        var canonical = string.Join(';', items) + ";";
        var canonicalBytes = System.Text.Encoding.UTF8.GetBytes(canonical);
        var canonicalHash = MD5.HashData(canonicalBytes);
        yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(canonicalHash));
        yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(canonicalHash).ToLowerInvariant());
        yield return canonicalHash;
        yield return canonicalBytes;

        foreach (var item in items)
        {
            yield return System.Text.Encoding.UTF8.GetBytes(item);
        }
    }

    private static IEnumerable<byte[]> BuildIntegrityExtensions(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("IntegrityCheck", out var integrity) || string.IsNullOrWhiteSpace(integrity))
        {
            yield break;
        }

        yield return System.Text.Encoding.ASCII.GetBytes(integrity);

        byte[] converted = null;
        try
        {
            converted = Convert.FromHexString(integrity);
        }
        catch (FormatException)
        {
        }
        if (converted != null)
        {
            yield return converted;
        }

        var md5 = MD5.HashData(System.Text.Encoding.ASCII.GetBytes(integrity));
        yield return md5;
        yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(md5));
        yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(md5).ToLowerInvariant());
    }

    private static IEnumerable<byte[]> BuildMetadataKeyExtensions(IReadOnlyDictionary<string, string> properties, bool exhaustive)
    {
        var seeds = new HashSet<string>(StringComparer.Ordinal);

        if (properties.TryGetValue("OrderXMLItem_PrimaryKey", out var primaryKey) && !string.IsNullOrWhiteSpace(primaryKey))
        {
            seeds.Add(primaryKey.Trim());
        }

        if (properties.TryGetValue("SourceApp", out var sourceApp) && !string.IsNullOrWhiteSpace(sourceApp))
        {
            var trimmed = sourceApp.Trim();
            seeds.Add(trimmed);

            var separators = new[] { ';', '#', '+', ',', ' ' };
            foreach (var token in trimmed.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Length >= 4)
                {
                    seeds.Add(token);
                }
            }

            var plusIndex = trimmed.IndexOf('+');
            if (plusIndex > 0)
            {
                seeds.Add(trimmed.Substring(0, plusIndex));
                if (plusIndex + 1 < trimmed.Length)
                {
                    seeds.Add(trimmed.Substring(plusIndex + 1));
                }
            }
        }

        foreach (var seed in seeds)
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes(seed);
            if (utf8.Length > 0 && utf8.Length <= 40)
            {
                yield return utf8;
            }

            if (!exhaustive)
            {
                continue;
            }

            var md5 = MD5.HashData(utf8);
            yield return md5;
            yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(md5));
            yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(md5).ToLowerInvariant());

            var sha1 = SHA1.HashData(utf8);
            yield return sha1;
            yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(sha1));
            yield return System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(sha1).ToLowerInvariant());

            if (seed.Length % 2 == 0)
            {
                byte[] rawHex = null;
                try
                {
                    rawHex = Convert.FromHexString(seed);
                }
                catch (FormatException)
                {
                }

                if (rawHex != null && rawHex.Length > 0 && rawHex.Length <= 40)
                {
                    yield return rawHex;
                }
            }
        }
    }

    private static void AddKeyCandidate(List<byte[]> results, HashSet<string> seen, List<byte> candidate)
    {
        if (candidate.Count < 4 || candidate.Count > 56)
        {
            return;
        }

        var bytes = candidate.ToArray();
        var key = Convert.ToHexString(bytes);
        if (seen.Add(key))
        {
            results.Add(bytes);
        }
    }

    private static void SwapEndiannessPerWordInBlocks(byte[] data)
    {
        for (var offset = 0; offset + 8 <= data.Length; offset += 8)
        {
            Array.Reverse(data, offset, 4);
            Array.Reverse(data, offset + 4, 4);
        }
    }

    private static uint ComputeAdler32(byte[] data)
    {
        uint a = 1;
        uint b = 0;

        foreach (var value in data)
        {
            a = (a + value) % AdlerMod;
            b = (b + a) % AdlerMod;
        }

        return (b << 16) | a;
    }

    private static uint SwapUInt32Endianness(uint value)
        => ((value & 0xFF000000U) >> 24) |
           ((value & 0x00FF0000U) >> 8) |
           ((value & 0x0000FF00U) << 8) |
           ((value & 0x000000FFU) << 24);

    private static void TryApplyBestCoordinateTransform(string filePath, List<Point3D> positions)
    {
        if (positions.Count == 0)
        {
            return;
        }

        List<XDocument> documents;
        try
        {
            documents = LoadDocumentHierarchy(filePath);
        }
        catch
        {
            return;
        }

        var commentOrigins = new List<Point3D>();
        var transforms = new List<CoordinateTransform>();

        foreach (var document in documents)
        {
            commentOrigins.AddRange(ExtractCommentOrigins(document));
            transforms.AddRange(ExtractCoordinateTransforms(document));
        }

        if (commentOrigins.Count == 0 || transforms.Count == 0)
        {
            return;
        }

        var baselineBounds = ComputeBounds(positions);
        var baselineScore = ScoreBoundsAgainstPoints(baselineBounds, commentOrigins);

        var bestTransform = default(CoordinateTransform);
        var bestInverse = false;
        var bestScore = baselineScore;

        foreach (var transform in transforms)
        {
            var forwardBounds = ComputeBounds(positions, transform, inverse: false);
            var forwardScore = ScoreBoundsAgainstPoints(forwardBounds, commentOrigins);
            if (forwardScore < bestScore)
            {
                bestScore = forwardScore;
                bestTransform = transform;
                bestInverse = false;
            }

            var inverseBounds = ComputeBounds(positions, transform, inverse: true);
            var inverseScore = ScoreBoundsAgainstPoints(inverseBounds, commentOrigins);
            if (inverseScore < bestScore)
            {
                bestScore = inverseScore;
                bestTransform = transform;
                bestInverse = true;
            }
        }

        // Only apply when we have a meaningful fit improvement.
        if (!(bestScore < baselineScore * 0.6 || baselineScore - bestScore > 2.0))
        {
            return;
        }

        for (var i = 0; i < positions.Count; i++)
        {
            positions[i] = bestInverse
                ? bestTransform.ApplyInverse(positions[i])
                : bestTransform.Apply(positions[i]);
        }
    }

    private static List<XDocument> LoadDocumentHierarchy(string filePath)
    {
        var root = XDocument.Load(filePath, LoadOptions.None);
        var documents = new List<XDocument> { root };
        var queue = new Queue<XDocument>();
        var visitedPayloads = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var payload in ExtractEmbeddedHpsPayloads(current))
            {
                if (!visitedPayloads.Add(payload))
                {
                    continue;
                }

                try
                {
                    var embedded = XDocument.Parse(payload, LoadOptions.None);
                    documents.Add(embedded);
                    queue.Enqueue(embedded);
                }
                catch
                {
                    // Ignore malformed embedded payloads.
                }
            }
        }

        return documents;
    }

    private static IEnumerable<Point3D> ExtractCommentOrigins(XDocument document)
    {
        foreach (var comment in document.Descendants().Where(element => element.Name.LocalName.Equals("Comment", StringComparison.OrdinalIgnoreCase)))
        {
            var origin = comment.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("Origin", StringComparison.OrdinalIgnoreCase));
            if (origin is null)
            {
                continue;
            }

            var x = ParseLocalDouble(origin, "x");
            var y = ParseLocalDouble(origin, "y");
            var z = ParseLocalDouble(origin, "z");
            if (x is null || y is null || z is null)
            {
                continue;
            }

            yield return new Point3D(x.Value, y.Value, z.Value);
        }
    }

    private static IEnumerable<CoordinateTransform> ExtractCoordinateTransforms(XDocument document)
    {
        foreach (var annotation in document.Descendants().Where(element => element.Name.LocalName.Equals("Annotation", StringComparison.OrdinalIgnoreCase)))
        {
            var type = annotation.Attribute("type")?.Value;
            if (!"CoordinateTransform".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var matrix = annotation.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("Matrix4x4", StringComparison.OrdinalIgnoreCase));
            if (matrix is null)
            {
                continue;
            }

            var m00 = ParseAttributeDouble(matrix, "m00");
            var m01 = ParseAttributeDouble(matrix, "m01");
            var m02 = ParseAttributeDouble(matrix, "m02");
            var m03 = ParseAttributeDouble(matrix, "m03");
            var m10 = ParseAttributeDouble(matrix, "m10");
            var m11 = ParseAttributeDouble(matrix, "m11");
            var m12 = ParseAttributeDouble(matrix, "m12");
            var m13 = ParseAttributeDouble(matrix, "m13");
            var m20 = ParseAttributeDouble(matrix, "m20");
            var m21 = ParseAttributeDouble(matrix, "m21");
            var m22 = ParseAttributeDouble(matrix, "m22");
            var m23 = ParseAttributeDouble(matrix, "m23");

            if (m00 is null || m01 is null || m02 is null || m03 is null ||
                m10 is null || m11 is null || m12 is null || m13 is null ||
                m20 is null || m21 is null || m22 is null || m23 is null)
            {
                continue;
            }

            yield return new CoordinateTransform(
                m00.Value, m01.Value, m02.Value, m03.Value,
                m10.Value, m11.Value, m12.Value, m13.Value,
                m20.Value, m21.Value, m22.Value, m23.Value);
        }
    }

    private static Rect3D ComputeBounds(IReadOnlyList<Point3D> points)
    {
        if (points.Count == 0)
        {
            return Rect3D.Empty;
        }

        var minX = points[0].X;
        var minY = points[0].Y;
        var minZ = points[0].Z;
        var maxX = points[0].X;
        var maxY = points[0].Y;
        var maxZ = points[0].Z;

        for (var i = 1; i < points.Count; i++)
        {
            var point = points[i];
            if (point.X < minX) minX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.Z < minZ) minZ = point.Z;
            if (point.X > maxX) maxX = point.X;
            if (point.Y > maxY) maxY = point.Y;
            if (point.Z > maxZ) maxZ = point.Z;
        }

        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    private static Rect3D ComputeBounds(IReadOnlyList<Point3D> points, CoordinateTransform transform, bool inverse)
    {
        if (points.Count == 0)
        {
            return Rect3D.Empty;
        }

        var first = inverse ? transform.ApplyInverse(points[0]) : transform.Apply(points[0]);
        var minX = first.X;
        var minY = first.Y;
        var minZ = first.Z;
        var maxX = first.X;
        var maxY = first.Y;
        var maxZ = first.Z;

        for (var i = 1; i < points.Count; i++)
        {
            var point = inverse ? transform.ApplyInverse(points[i]) : transform.Apply(points[i]);
            if (point.X < minX) minX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.Z < minZ) minZ = point.Z;
            if (point.X > maxX) maxX = point.X;
            if (point.Y > maxY) maxY = point.Y;
            if (point.Z > maxZ) maxZ = point.Z;
        }

        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    private static double ScoreBoundsAgainstPoints(Rect3D bounds, IReadOnlyList<Point3D> points)
    {
        if (bounds.IsEmpty || points.Count == 0)
        {
            return double.PositiveInfinity;
        }

        var maxX = bounds.X + bounds.SizeX;
        var maxY = bounds.Y + bounds.SizeY;
        var maxZ = bounds.Z + bounds.SizeZ;
        var total = 0.0;

        foreach (var point in points)
        {
            var dx = point.X < bounds.X
                ? bounds.X - point.X
                : point.X > maxX
                    ? point.X - maxX
                    : 0.0;
            var dy = point.Y < bounds.Y
                ? bounds.Y - point.Y
                : point.Y > maxY
                    ? point.Y - maxY
                    : 0.0;
            var dz = point.Z < bounds.Z
                ? bounds.Z - point.Z
                : point.Z > maxZ
                    ? point.Z - maxZ
                    : 0.0;

            total += Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        return total / points.Count;
    }

    private static double? ParseLocalDouble(XElement element, string localName)
    {
        var child = element.Elements().FirstOrDefault(candidate => candidate.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
        if (child is null)
        {
            return null;
        }

        return double.TryParse(child.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? ParseAttributeDouble(XElement element, string attributeName)
    {
        var raw = element.Attribute(attributeName)?.Value;
        if (raw is null)
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private readonly struct CoordinateTransform
    {
        private readonly double _m00;
        private readonly double _m01;
        private readonly double _m02;
        private readonly double _m03;
        private readonly double _m10;
        private readonly double _m11;
        private readonly double _m12;
        private readonly double _m13;
        private readonly double _m20;
        private readonly double _m21;
        private readonly double _m22;
        private readonly double _m23;

        public CoordinateTransform(
            double m00, double m01, double m02, double m03,
            double m10, double m11, double m12, double m13,
            double m20, double m21, double m22, double m23)
        {
            _m00 = m00;
            _m01 = m01;
            _m02 = m02;
            _m03 = m03;
            _m10 = m10;
            _m11 = m11;
            _m12 = m12;
            _m13 = m13;
            _m20 = m20;
            _m21 = m21;
            _m22 = m22;
            _m23 = m23;
        }

        public Point3D Apply(Point3D point)
        {
            var x = _m00 * point.X + _m01 * point.Y + _m02 * point.Z + _m03;
            var y = _m10 * point.X + _m11 * point.Y + _m12 * point.Z + _m13;
            var z = _m20 * point.X + _m21 * point.Y + _m22 * point.Z + _m23;
            return new Point3D(x, y, z);
        }

        public Point3D ApplyInverse(Point3D point)
        {
            var px = point.X - _m03;
            var py = point.Y - _m13;
            var pz = point.Z - _m23;

            // 3Shape transform blocks are rigid transforms, so inverse rotation is transpose.
            var x = _m00 * px + _m10 * py + _m20 * pz;
            var y = _m01 * px + _m11 * py + _m21 * pz;
            var z = _m02 * px + _m12 * py + _m22 * pz;
            return new Point3D(x, y, z);
        }
    }

    private sealed record DcmMetadata(string Schema, IReadOnlyDictionary<string, string> Properties);

    private readonly record struct Edge(int Start, int End);

    private readonly record struct Triangle(int V0, int V1, int V2);

    private readonly record struct PendingFacets(byte[] Bytes, int FaceCount);

    private readonly record struct FacetDecodeScore(int ExpectedDelta, double OutOfRangeRatio, double MaxFanRatio) : IComparable<FacetDecodeScore>
    {
        public static FacetDecodeScore Worst => new(int.MaxValue, double.PositiveInfinity, double.PositiveInfinity);

        public int CompareTo(FacetDecodeScore other)
        {
            var compareExpected = ExpectedDelta.CompareTo(other.ExpectedDelta);
            if (compareExpected != 0)
            {
                return compareExpected;
            }

            var compareOutOfRange = OutOfRangeRatio.CompareTo(other.OutOfRangeRatio);
            if (compareOutOfRange != 0)
            {
                return compareOutOfRange;
            }

            return MaxFanRatio.CompareTo(other.MaxFanRatio);
        }
    }

    private static List<Point3D> SelectBestPointSet(List<Point3D> absolute, List<Point3D> delta)
    {
        var absoluteScore = ScorePointSet(absolute);
        var deltaScore = ScorePointSet(delta);

        return deltaScore < absoluteScore ? delta : absolute;
    }

    private static double ScorePointSet(List<Point3D> points)
    {
        if (points.Count < 3)
        {
            return double.MaxValue;
        }

        var minX = points[0].X;
        var minY = points[0].Y;
        var minZ = points[0].Z;
        var maxX = minX;
        var maxY = minY;
        var maxZ = minZ;

        for (var i = 1; i < points.Count; i++)
        {
            var p = points[i];
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            minZ = Math.Min(minZ, p.Z);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
            maxZ = Math.Max(maxZ, p.Z);
        }

        var sizeX = maxX - minX;
        var sizeY = maxY - minY;
        var sizeZ = maxZ - minZ;

        if (!double.IsFinite(sizeX) || !double.IsFinite(sizeY) || !double.IsFinite(sizeZ) ||
            sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            return double.MaxValue;
        }

        var diagonal = Math.Sqrt((sizeX * sizeX) + (sizeY * sizeY) + (sizeZ * sizeZ));
        if (!double.IsFinite(diagonal) || diagonal <= 0)
        {
            return double.MaxValue;
        }

        var minSize = Math.Min(sizeX, Math.Min(sizeY, sizeZ));
        var maxSize = Math.Max(sizeX, Math.Max(sizeY, sizeZ));
        var aspectRatio = minSize <= 0 ? double.PositiveInfinity : (maxSize / minSize);

        var maxAbs = Math.Max(
            Math.Max(Math.Abs(minX), Math.Abs(maxX)),
            Math.Max(
                Math.Max(Math.Abs(minY), Math.Abs(maxY)),
                Math.Max(Math.Abs(minZ), Math.Abs(maxZ))));

        // Prefer compact, non-stretched, reasonably-centered coordinate sets.
        // Rod-like decode artifacts usually produce extreme aspect ratios and large offsets.
        var score = diagonal;

        if (aspectRatio > 3.5)
        {
            var excess = aspectRatio - 3.5;
            score += excess * excess * 30.0;
        }

        if (maxAbs > 2000.0)
        {
            score += (maxAbs - 2000.0) * 0.01;
        }

        return score;
    }
}
