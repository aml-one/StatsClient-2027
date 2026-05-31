using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace DCMViewer.Services;

internal static class DcmMeshWriter
{
    private const int PointStrideBytes = 12;
    private const uint AdlerMod = 65521;

    public static void SaveVerticesToDcm(string filePath, IReadOnlyList<Point3D> worldPositions, DcmMeshWriteProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(worldPositions);
        ArgumentNullException.ThrowIfNull(profile);

        if (!string.Equals(Path.GetExtension(filePath), ".dcm", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only .dcm files can be saved in place.");
        }

        if (worldPositions.Count != profile.VertexCount)
        {
            throw new InvalidOperationException(
                $"Vertex count changed ({worldPositions.Count} vs {profile.VertexCount}). Save is only supported when topology is unchanged.");
        }

        if (profile.IsEncrypted && profile.Encryption is null)
        {
            throw new InvalidOperationException(
                "This encrypted DCM could not be prepared for write-back. Export STL instead, or re-open the case after a successful decrypt.");
        }

        var fileSpace = ConvertWorldToFileSpace(worldPositions, profile.SceneTransform);
        var rawBytes = EncodeVertices(fileSpace, profile.UseDeltaEncoding);
        var payloadBytes = profile.IsEncrypted
            ? EncryptCeBuffer(rawBytes, profile.Encryption!)
            : rawBytes;

        var backupPath = filePath + ".bak";
        File.Copy(filePath, backupPath, overwrite: true);

        try
        {
            var document = XDocument.Load(filePath, LoadOptions.None);
            var verticesElement = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals("Vertices", StringComparison.OrdinalIgnoreCase));
            if (verticesElement is null)
            {
                throw new InvalidOperationException("No <Vertices> element found in the DCM file.");
            }

            var checkValue = ComputeAdler32(rawBytes);
            verticesElement.SetAttributeValue("check_value", checkValue.ToString(CultureInfo.InvariantCulture));

            if (profile.VerticesStoredAsBase64)
            {
                verticesElement.Value = Convert.ToBase64String(payloadBytes);
            }
            else
            {
                verticesElement.Value = FormatPlainFloatVertices(payloadBytes);
            }

            document.Save(filePath);
        }
        catch
        {
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, filePath, overwrite: true);
            }

            throw;
        }
    }

    private static IReadOnlyList<Point3D> ConvertWorldToFileSpace(
        IReadOnlyList<Point3D> worldPositions,
        SceneTransformProfile? sceneTransform)
    {
        if (sceneTransform is null)
        {
            return worldPositions.ToArray();
        }

        var transform = sceneTransform;
        var converted = new Point3D[worldPositions.Count];
        for (var index = 0; index < worldPositions.Count; index++)
        {
            converted[index] = transform.AppliedInverse
                ? ApplyForward(worldPositions[index], transform)
                : ApplyInverse(worldPositions[index], transform);
        }

        return converted;
    }

    private static Point3D ApplyForward(Point3D point, SceneTransformProfile transform)
    {
        if (transform.UseColumnMajor)
        {
            var x = transform.M00 * point.X + transform.M10 * point.Y + transform.M20 * point.Z + transform.M03;
            var y = transform.M01 * point.X + transform.M11 * point.Y + transform.M21 * point.Z + transform.M13;
            var z = transform.M02 * point.X + transform.M12 * point.Y + transform.M22 * point.Z + transform.M23;
            return new Point3D(x, y, z);
        }

        var fx = transform.M00 * point.X + transform.M01 * point.Y + transform.M02 * point.Z + transform.M03;
        var fy = transform.M10 * point.X + transform.M11 * point.Y + transform.M12 * point.Z + transform.M13;
        var fz = transform.M20 * point.X + transform.M21 * point.Y + transform.M22 * point.Z + transform.M23;
        return new Point3D(fx, fy, fz);
    }

    private static Point3D ApplyInverse(Point3D point, SceneTransformProfile transform)
    {
        var px = point.X - transform.M03;
        var py = point.Y - transform.M13;
        var pz = point.Z - transform.M23;

        if (transform.UseColumnMajor)
        {
            var x = transform.M00 * px + transform.M01 * py + transform.M02 * pz;
            var y = transform.M10 * px + transform.M11 * py + transform.M12 * pz;
            var z = transform.M20 * px + transform.M21 * py + transform.M22 * pz;
            return new Point3D(x, y, z);
        }

        var fx = transform.M00 * px + transform.M10 * py + transform.M20 * pz;
        var fy = transform.M01 * px + transform.M11 * py + transform.M21 * pz;
        var fz = transform.M02 * px + transform.M12 * py + transform.M22 * pz;
        return new Point3D(fx, fy, fz);
    }

    private static byte[] EncodeVertices(IReadOnlyList<Point3D> fileSpacePositions, bool useDeltaEncoding)
    {
        var bytes = new byte[fileSpacePositions.Count * PointStrideBytes];
        float prevX = 0;
        float prevY = 0;
        float prevZ = 0;

        for (var index = 0; index < fileSpacePositions.Count; index++)
        {
            var point = fileSpacePositions[index];
            var absX = (float)point.X;
            var absY = (float)point.Y;
            var absZ = (float)point.Z;
            var writeX = absX;
            var writeY = absY;
            var writeZ = absZ;

            if (useDeltaEncoding)
            {
                writeX = absX - prevX;
                writeY = absY - prevY;
                writeZ = absZ - prevZ;
            }

            var offset = index * PointStrideBytes;
            BitConverter.TryWriteBytes(bytes.AsSpan(offset, 4), writeX);
            BitConverter.TryWriteBytes(bytes.AsSpan(offset + 4, 4), writeY);
            BitConverter.TryWriteBytes(bytes.AsSpan(offset + 8, 4), writeZ);
            prevX = absX;
            prevY = absY;
            prevZ = absZ;
        }

        return bytes;
    }

    private static byte[] EncryptCeBuffer(byte[] rawBytes, CeEncryptionProfile profile)
    {
        var padded = (byte[])rawBytes.Clone();
        if (padded.Length % 8 != 0)
        {
            Array.Resize(ref padded, padded.Length + (8 - (padded.Length % 8)));
        }

        if (profile.PreSwap)
        {
            SwapEndiannessPerWordInBlocks(padded);
        }

        var engine = new BlowfishEngine();
        engine.Init(true, new KeyParameter(profile.Key));

        var encrypted = new byte[padded.Length];
        for (var offset = 0; offset < padded.Length; offset += engine.GetBlockSize())
        {
            engine.ProcessBlock(padded, offset, encrypted, offset);
        }

        if (profile.PostSwap)
        {
            SwapEndiannessPerWordInBlocks(encrypted);
        }

        return encrypted;
    }

    private static string FormatPlainFloatVertices(byte[] rawBytes)
    {
        var builder = new StringBuilder(rawBytes.Length * 2);
        for (var offset = 0; offset + PointStrideBytes <= rawBytes.Length; offset += PointStrideBytes)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(BitConverter.ToSingle(rawBytes, offset).ToString("R", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(BitConverter.ToSingle(rawBytes, offset + 4).ToString("R", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(BitConverter.ToSingle(rawBytes, offset + 8).ToString("R", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
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

    private static void SwapEndiannessPerWordInBlocks(byte[] data)
    {
        for (var offset = 0; offset + 4 <= data.Length; offset += 4)
        {
            (data[offset], data[offset + 3]) = (data[offset + 3], data[offset]);
            (data[offset + 1], data[offset + 2]) = (data[offset + 2], data[offset + 1]);
        }
    }
}
