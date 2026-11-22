using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Bitmap = System.Drawing.Bitmap;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Reads colored calibration rectangles from reference PNG images.
/// Red = pan, Blue = RUSH, Green = info text.
/// </summary>
public static class PrescriptionStampCalibrationReader
{
    private enum MarkerColor
    {
        Red,
        Blue,
        Green,
    }

    private sealed class Component
    {
        public int MinX = int.MaxValue;
        public int MinY = int.MaxValue;
        public int MaxX = int.MinValue;
        public int MaxY = int.MinValue;
        public int PixelCount;

        public void Include(int x, int y)
        {
            PixelCount++;
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
        }

        public void Merge(Component other)
        {
            PixelCount += other.PixelCount;
            MinX = Math.Min(MinX, other.MinX);
            MinY = Math.Min(MinY, other.MinY);
            MaxX = Math.Max(MaxX, other.MaxX);
            MaxY = Math.Max(MaxY, other.MaxY);
        }

        public int Width => Math.Max(1, MaxX - MinX + 1);
        public int Height => Math.Max(1, MaxY - MinY + 1);
    }

    public static PrescriptionStampProfile ReadProfile(string pageHeaderIsHigh, string calibrationImagePath)
    {
        if (!File.Exists(calibrationImagePath))
        {
            throw new FileNotFoundException($"Stamp calibration image not found: {calibrationImagePath}");
        }

        using var bitmap = new Bitmap(calibrationImagePath);
        FieldRegion pan = ReadMarkerRegion(bitmap, MarkerColor.Red, "PanNumber");
        FieldRegion rush = ReadMarkerRegion(bitmap, MarkerColor.Blue, "Rush");
        FieldRegion info = ReadMarkerRegion(bitmap, MarkerColor.Green, "InfoText");

        return new PrescriptionStampProfile(
            pageHeaderIsHigh,
            Path.GetFileName(calibrationImagePath),
            bitmap.Width,
            bitmap.Height,
            pan,
            rush,
            info);
    }

    private static FieldRegion ReadMarkerRegion(Bitmap bitmap, MarkerColor color, string name)
    {
        bool[,] marker = BuildMarkerMask(bitmap, color);
        List<Component> components = FindComponents(marker, bitmap.Width, bitmap.Height);
        if (components.Count == 0)
        {
            throw new InvalidOperationException($"No {color} calibration rectangle found for {name}.");
        }

        List<Component> merged = MergeNearbyComponents(components, mergeGapPixels: 14);
        Component best = PickBestComponent(merged, bitmap.Width, bitmap.Height);
        if (best.PixelCount < 25)
        {
            throw new InvalidOperationException($"No {color} calibration rectangle found for {name} ({best.PixelCount} pixels).");
        }

        double w = bitmap.Width;
        double h = bitmap.Height;
        return new FieldRegion(
            name,
            best.MinX / w,
            best.MinY / h,
            best.Width / w,
            best.Height / h);
    }

    private static bool[,] BuildMarkerMask(Bitmap bitmap, MarkerColor color)
    {
        var mask = new bool[bitmap.Width, bitmap.Height];
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            int stride = Math.Abs(data.Stride);
            int height = bitmap.Height;
            int width = bitmap.Width;
            var pixels = new byte[stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int i = rowStart + (x * 4);
                    var c = Color.FromArgb(pixels[i + 3], pixels[i + 2], pixels[i + 1], pixels[i]);
                    if (MatchesMarker(c, color))
                    {
                        mask[x, y] = true;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return mask;
    }

    private static List<Component> FindComponents(bool[,] marker, int width, int height)
    {
        var visited = new bool[width, height];
        var components = new List<Component>();
        int[] dx = [1, -1, 0, 0, 1, 1, -1, -1];
        int[] dy = [0, 0, 1, -1, 1, -1, 1, -1];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!marker[x, y] || visited[x, y])
                {
                    continue;
                }

                var component = new Component();
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y));
                visited[x, y] = true;

                while (queue.Count > 0)
                {
                    (int cx, int cy) = queue.Dequeue();
                    component.Include(cx, cy);

                    for (int i = 0; i < dx.Length; i++)
                    {
                        int nx = cx + dx[i];
                        int ny = cy + dy[i];
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        if (!marker[nx, ny] || visited[nx, ny])
                        {
                            continue;
                        }

                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }

                components.Add(component);
            }
        }

        return components;
    }

    private static List<Component> MergeNearbyComponents(List<Component> components, int mergeGapPixels)
    {
        var merged = new List<Component>(components.Select(CloneComponent));
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < merged.Count; i++)
            {
                for (int j = i + 1; j < merged.Count; j++)
                {
                    if (!BoundsOverlap(merged[i], merged[j], mergeGapPixels))
                    {
                        continue;
                    }

                    merged[i].Merge(merged[j]);
                    merged.RemoveAt(j);
                    changed = true;
                    break;
                }

                if (changed)
                {
                    break;
                }
            }
        }
        while (changed);

        return merged;
    }

    private static Component PickBestComponent(List<Component> components, int imageWidth, int imageHeight)
    {
        IEnumerable<Component> candidates = components.Where(c => c.PixelCount >= 25);
        List<Component> reasonable = candidates
            .Where(c => IsReasonableStampBox(c.Width / (double)imageWidth, c.Height / (double)imageHeight))
            .ToList();

        if (reasonable.Count > 0)
        {
            return reasonable
                .OrderByDescending(c => ScoreComponent(c, imageWidth, imageHeight))
                .First();
        }

        // RUSH boxes should not be the full-width page header strip.
        List<Component> notTopStrip = candidates
            .Where(c => c.MinY > 2 && c.Width < imageWidth * 0.85)
            .ToList();
        if (notTopStrip.Count > 0)
        {
            return notTopStrip
                .OrderByDescending(c => ScoreComponent(c, imageWidth, imageHeight))
                .First();
        }

        return candidates
            .OrderByDescending(c => ScoreComponent(c, imageWidth, imageHeight))
            .First();
    }

    private static double ScoreComponent(Component component, int imageWidth, int imageHeight)
    {
        double normWidth = component.Width / (double)imageWidth;
        double normHeight = component.Height / (double)imageHeight;
        double normArea = normWidth * normHeight;
        double fill = component.PixelCount / (double)(component.Width * component.Height);

        double sizeScore = IsReasonableStampBox(normWidth, normHeight) ? 1_000_000 : 1;
        double compactPenalty = normArea > 0.12 ? 0.0001 : 1;
        double strokeBonus = fill is >= 0.02 and <= 0.45 ? 4 : 1;
        double fillBonus = fill >= 0.55 ? 3 : 1;

        return component.PixelCount * sizeScore * compactPenalty * Math.Max(strokeBonus, fillBonus);
    }

    private static bool IsReasonableStampBox(double normWidth, double normHeight) =>
        normWidth is >= 0.04 and <= 0.75
        && normHeight is >= 0.015 and <= 0.12;

    private static bool BoundsOverlap(Component a, Component b, int gap)
    {
        int ax1 = a.MinX - gap;
        int ay1 = a.MinY - gap;
        int ax2 = a.MaxX + gap;
        int ay2 = a.MaxY + gap;
        int bx1 = b.MinX - gap;
        int by1 = b.MinY - gap;
        int bx2 = b.MaxX + gap;
        int by2 = b.MaxY + gap;
        return ax1 <= bx2 && bx1 <= ax2 && ay1 <= by2 && by1 <= ay2;
    }

    private static Component CloneComponent(Component source) =>
        new()
        {
            MinX = source.MinX,
            MinY = source.MinY,
            MaxX = source.MaxX,
            MaxY = source.MaxY,
            PixelCount = source.PixelCount,
        };

    private static bool MatchesMarker(Color c, MarkerColor color) =>
        color switch
        {
            // User calibration strokes (~#EF4444, ~#3B82F6, ~#10B981).
            MarkerColor.Red => IsNear(c, 239, 68, 68, 32)
                || (c.R >= 210 && c.G <= 95 && c.B <= 95 && c.R - Math.Max(c.G, c.B) >= 80),
            MarkerColor.Blue => (IsNear(c, 59, 130, 246, 28) && c.R <= 95)
                || (c.B >= 210 && c.R <= 65 && c.G is >= 90 and <= 150 && c.B - c.R >= 150),
            MarkerColor.Green => IsNear(c, 16, 185, 129, 32)
                || (c.G >= 170 && c.R <= 60 && c.B is >= 90 and <= 170 && c.G - c.R >= 90),
            _ => false,
        };

    private static bool IsNear(Color c, byte r, byte g, byte b, int tolerance) =>
        Math.Abs(c.R - r) <= tolerance
        && Math.Abs(c.G - g) <= tolerance
        && Math.Abs(c.B - b) <= tolerance;
}
