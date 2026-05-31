using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Estimates a "facial" (buccal) view direction from scan mesh geometry so the default camera
/// looks at the tooth surfaces rather than from an arbitrary axis (+Z).
/// </summary>
internal static class DentalCameraOrientationHelper
{
    private const int MaxSampledTriangles = 48_000;
    private const double MinConfidence = 0.08;
    private const double FrontViewAlignmentThreshold = 0.55;
    private const double FrontViewScoreRatio = 0.82;

    /// <summary>Default "front" in scene space: camera on +Z looking toward the arch.</summary>
    public static readonly Vector3D DefaultFrontFacialDirection = new(0, 0, 1);

    /// <summary>
    /// True when +Z already shows buccal surfaces about as well as the estimated facial axis.
    /// </summary>
    public static bool IsFrontViewBuccalAdequate(
        IReadOnlyList<MeshSnapshot> meshes,
        Rect3D bounds,
        Vector3D frontFacialDirection)
    {
        if (meshes.Count == 0 || bounds.IsEmpty)
        {
            return true;
        }

        frontFacialDirection.Normalize();
        if (!TryEstimateFacialView(meshes, bounds, out var estimatedFacial, out _))
        {
            return true;
        }

        estimatedFacial.Normalize();
        var alignment = Math.Abs(Vector3D.DotProduct(estimatedFacial, frontFacialDirection));
        if (alignment < FrontViewAlignmentThreshold)
        {
            return false;
        }

        var centroid = GetBoundsCenter(bounds);
        var frontScore = ScoreViewDirection(meshes, centroid, frontFacialDirection);
        var estimatedScore = ScoreViewDirection(meshes, centroid, estimatedFacial);
        if (estimatedScore < 1e-9)
        {
            return alignment >= FrontViewAlignmentThreshold;
        }

        return frontScore >= estimatedScore * FrontViewScoreRatio;
    }

    public static Vector3D OrthogonalizeUp(Vector3D up, Vector3D lookDirection)
    {
        if (lookDirection.LengthSquared < 1e-9)
        {
            return up.LengthSquared > 1e-9 ? up : new Vector3D(0, 1, 0);
        }

        lookDirection.Normalize();
        var upOrtho = up - (lookDirection * Vector3D.DotProduct(up, lookDirection));
        if (upOrtho.LengthSquared < 1e-9)
        {
            return new Vector3D(0, 1, 0);
        }

        upOrtho.Normalize();
        return upOrtho;
    }

    /// <summary>
    /// <paramref name="facialDirection"/> points from the scene look-at center toward the camera (buccal/outward).
    /// <paramref name="upDirection"/> points toward the occlusal side (crown tips), orthogonal to facial.
    /// </summary>
    public static bool TryEstimateFacialView(
        IReadOnlyList<MeshSnapshot> meshes,
        Rect3D bounds,
        out Vector3D facialDirection,
        out Vector3D upDirection)
    {
        facialDirection = new Vector3D(0, 0, 1);
        upDirection = new Vector3D(0, 1, 0);

        if (meshes.Count == 0 || bounds.IsEmpty)
        {
            return false;
        }

        var centroid = GetBoundsCenter(bounds);
        var occlusalAxis = EstimateOcclusalAxis(meshes, bounds, centroid);
        if (!TryEstimateOutwardFacialDirection(meshes, centroid, occlusalAxis, out var outward))
        {
            outward = GuessFacialFromBounds(bounds, occlusalAxis);
        }

        outward = ProjectOntoPlane(outward, occlusalAxis);
        if (outward.LengthSquared < 1e-9)
        {
            return false;
        }

        outward.Normalize();
        occlusalAxis.Normalize();

        upDirection = occlusalAxis;
        facialDirection = outward;
        return true;
    }

    private static Point3D GetBoundsCenter(Rect3D bounds)
        => new(
            bounds.X + bounds.SizeX * 0.5,
            bounds.Y + bounds.SizeY * 0.5,
            bounds.Z + bounds.SizeZ * 0.5);

    /// <summary>
    /// Smallest bounding-box extent is usually occlusal–gingival; refine sign with crown-side normal voting.
    /// </summary>
    private static Vector3D EstimateOcclusalAxis(IReadOnlyList<MeshSnapshot> meshes, Rect3D bounds, Point3D centroid)
    {
        var axis = SmallestExtentAxis(bounds);
        var positive = axis;
        var negative = Invert(axis);

        var positiveScore = ScoreNormalsAlongAxis(meshes, centroid, positive);
        var negativeScore = ScoreNormalsAlongAxis(meshes, centroid, negative);
        return positiveScore >= negativeScore ? positive : negative;
    }

    private static Vector3D SmallestExtentAxis(Rect3D bounds)
    {
        if (bounds.SizeY <= bounds.SizeX && bounds.SizeY <= bounds.SizeZ)
        {
            return new Vector3D(0, 1, 0);
        }

        if (bounds.SizeZ <= bounds.SizeX && bounds.SizeZ <= bounds.SizeY)
        {
            return new Vector3D(0, 0, 1);
        }

        return new Vector3D(1, 0, 0);
    }

    private static double ScoreNormalsAlongAxis(IReadOnlyList<MeshSnapshot> meshes, Point3D centroid, Vector3D axis)
    {
        axis.Normalize();
        var score = 0.0;
        var sampled = 0;

        foreach (var mesh in meshes)
        {
            foreach (var (normal, center, area) in SampleTriangles(mesh))
            {
                var toTriangle = center - centroid;
                if (toTriangle.LengthSquared < 1e-12)
                {
                    continue;
                }

                toTriangle.Normalize();
                if (Vector3D.DotProduct(normal, toTriangle) < 0.15)
                {
                    continue;
                }

                score += area * Math.Max(0, Vector3D.DotProduct(normal, axis));
                sampled++;
                if (sampled >= MaxSampledTriangles)
                {
                    return score;
                }
            }
        }

        return score;
    }

    private static bool TryEstimateOutwardFacialDirection(
        IReadOnlyList<MeshSnapshot> meshes,
        Point3D centroid,
        Vector3D occlusalAxis,
        out Vector3D outward)
    {
        outward = default;
        var sum = new Vector3D();
        var weight = 0.0;
        var sampled = 0;

        foreach (var mesh in meshes)
        {
            foreach (var (normal, center, area) in SampleTriangles(mesh))
            {
                var toTriangle = center - centroid;
                if (toTriangle.LengthSquared < 1e-12)
                {
                    continue;
                }

                toTriangle.Normalize();
                var facingOutward = Vector3D.DotProduct(normal, toTriangle);
                if (facingOutward < 0.2)
                {
                    continue;
                }

                var planar = ProjectOntoPlane(normal, occlusalAxis);
                if (planar.LengthSquared < 1e-9)
                {
                    continue;
                }

                sum += planar * (area * facingOutward);
                weight += area * facingOutward;
                sampled++;
                if (sampled >= MaxSampledTriangles)
                {
                    break;
                }
            }
        }

        if (weight < 1e-9 || sum.LengthSquared < 1e-9)
        {
            return false;
        }

        outward = sum;
        var confidence = outward.Length / weight;
        return confidence >= MinConfidence;
    }

    private static double ScoreViewDirection(IReadOnlyList<MeshSnapshot> meshes, Point3D centroid, Vector3D viewFromCamera)
    {
        viewFromCamera.Normalize();
        var score = 0.0;
        var sampled = 0;

        foreach (var mesh in meshes)
        {
            foreach (var (normal, center, area) in SampleTriangles(mesh))
            {
                var toTriangle = center - centroid;
                if (toTriangle.LengthSquared < 1e-12)
                {
                    continue;
                }

                toTriangle.Normalize();
                var facingCamera = Vector3D.DotProduct(normal, viewFromCamera);
                if (facingCamera < 0.2)
                {
                    continue;
                }

                score += area * facingCamera;
                sampled++;
                if (sampled >= MaxSampledTriangles)
                {
                    return score;
                }
            }
        }

        return score;
    }

    private static Vector3D GuessFacialFromBounds(Rect3D bounds, Vector3D occlusalAxis)
    {
        var axis = LargestExtentAxis(bounds);
        var facial = Vector3D.CrossProduct(occlusalAxis, axis);
        if (facial.LengthSquared < 1e-9)
        {
            facial = Vector3D.CrossProduct(occlusalAxis, new Vector3D(0, 0, 1));
        }

        return facial;
    }

    private static Vector3D LargestExtentAxis(Rect3D bounds)
    {
        if (bounds.SizeX >= bounds.SizeY && bounds.SizeX >= bounds.SizeZ)
        {
            return new Vector3D(1, 0, 0);
        }

        if (bounds.SizeY >= bounds.SizeX && bounds.SizeY >= bounds.SizeZ)
        {
            return new Vector3D(0, 1, 0);
        }

        return new Vector3D(0, 0, 1);
    }

    private static Vector3D ProjectOntoPlane(Vector3D vector, Vector3D planeNormal)
    {
        planeNormal.Normalize();
        return vector - (planeNormal * Vector3D.DotProduct(vector, planeNormal));
    }

    private static Vector3D Invert(Vector3D vector) => new(-vector.X, -vector.Y, -vector.Z);

    private static IEnumerable<(Vector3D Normal, Point3D Center, double Area)> SampleTriangles(MeshSnapshot mesh)
    {
        var positions = mesh.Positions;
        var indices = mesh.TriangleIndices;
        if (positions.Length < 3)
        {
            yield break;
        }

        var triangleCount = indices.Length >= 3 ? indices.Length / 3 : positions.Length / 3;
        if (triangleCount <= 0)
        {
            yield break;
        }

        var stride = triangleCount <= MaxSampledTriangles
            ? 1
            : (int)Math.Ceiling((double)triangleCount / MaxSampledTriangles);

        if (indices.Length >= 3)
        {
            for (var t = 0; t < triangleCount; t += stride)
            {
                var i0 = indices[t * 3];
                var i1 = indices[t * 3 + 1];
                var i2 = indices[t * 3 + 2];
                if (!TryGetTriangle(positions, i0, i1, i2, out var p0, out var p1, out var p2))
                {
                    continue;
                }

                yield return BuildTriangleInfo(p0, p1, p2);
            }

            yield break;
        }

        for (var t = 0; t + 2 < positions.Length; t += stride * 3)
        {
            yield return BuildTriangleInfo(positions[t], positions[t + 1], positions[t + 2]);
        }
    }

    private static bool TryGetTriangle(
        Point3D[] positions,
        int i0,
        int i1,
        int i2,
        out Point3D p0,
        out Point3D p1,
        out Point3D p2)
    {
        p0 = default;
        p1 = default;
        p2 = default;
        if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
        {
            return false;
        }

        p0 = positions[i0];
        p1 = positions[i1];
        p2 = positions[i2];
        return true;
    }

    private static (Vector3D Normal, Point3D Center, double Area) BuildTriangleInfo(Point3D p0, Point3D p1, Point3D p2)
    {
        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        var cross = Vector3D.CrossProduct(edge1, edge2);
        var area = cross.Length * 0.5;
        if (area < 1e-12)
        {
            return (new Vector3D(0, 0, 1), new Point3D(
                (p0.X + p1.X + p2.X) / 3.0,
                (p0.Y + p1.Y + p2.Y) / 3.0,
                (p0.Z + p1.Z + p2.Z) / 3.0), 0);
        }

        cross.Normalize();
        var center = new Point3D(
            (p0.X + p1.X + p2.X) / 3.0,
            (p0.Y + p1.Y + p2.Y) / 3.0,
            (p0.Z + p1.Z + p2.Z) / 3.0);
        return (cross, center, area);
    }
}
