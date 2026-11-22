using System.Windows;
using System.Windows.Media;

namespace StatsClient.MVVM.Core;

public static class TeethSelectorGeometry
{
    private enum ToothKind
    {
        Molar,
        Premolar,
        Canine,
        Incisor,
    }

    public static PathGeometry CreateCrown(TeethSelectorSlot slot, double canvasWidth, double canvasHeight) =>
        BuildCrownFigure(slot, canvasWidth, canvasHeight, includeRoot: false);

    public static PathGeometry CreateRoot(TeethSelectorSlot slot, double canvasWidth, double canvasHeight) =>
        BuildCrownFigure(slot, canvasWidth, canvasHeight, includeRoot: true, rootOnly: true);

    public static PathGeometry CreateUpperGumLine(double canvasWidth, double canvasHeight)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(canvasWidth * 0.04, canvasHeight * 0.495),
            IsClosed = false,
        };
        figure.Segments.Add(new BezierSegment(
            new Point(canvasWidth * 0.28, canvasHeight * 0.455),
            new Point(canvasWidth * 0.72, canvasHeight * 0.455),
            new Point(canvasWidth * 0.96, canvasHeight * 0.495),
            true));
        return new PathGeometry { Figures = { figure } };
    }

    public static PathGeometry CreateLowerGumLine(double canvasWidth, double canvasHeight)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(canvasWidth * 0.04, canvasHeight * 0.505),
            IsClosed = false,
        };
        figure.Segments.Add(new BezierSegment(
            new Point(canvasWidth * 0.28, canvasHeight * 0.545),
            new Point(canvasWidth * 0.72, canvasHeight * 0.545),
            new Point(canvasWidth * 0.96, canvasHeight * 0.505),
            true));
        return new PathGeometry { Figures = { figure } };
    }

    private static PathGeometry BuildCrownFigure(
        TeethSelectorSlot slot,
        double canvasWidth,
        double canvasHeight,
        bool includeRoot,
        bool rootOnly = false)
    {
        bool isUpper = slot.ToothNumber <= 16;
        double cx = slot.CenterX * canvasWidth;
        double cy = slot.CenterY * canvasHeight;
        double w = slot.PixelWidth(canvasWidth);
        double h = slot.PixelHeight(canvasHeight);
        ToothKind kind = ResolveKind(slot.ToothNumber);

        double cervicalHalf = w * (kind switch
        {
            ToothKind.Molar => 0.32,
            ToothKind.Premolar => 0.27,
            ToothKind.Canine => 0.25,
            _ => 0.24,
        });
        double occlusalHalf = w * (kind switch
        {
            ToothKind.Molar => 0.44,
            ToothKind.Premolar => 0.37,
            ToothKind.Canine => 0.28,
            _ => 0.31,
        });

        double crownDepth = h * (kind switch
        {
            ToothKind.Molar => 0.36,
            ToothKind.Canine => 0.34,
            ToothKind.Incisor => 0.32,
            _ => 0.33,
        });
        double baseCrownDepth = crownDepth;
        double rootDepth = h * 0.38;

        ApplyToothAdjustments(slot.ToothNumber, ref cervicalHalf, ref occlusalHalf, ref crownDepth);

        if (rootOnly)
        {
            return BuildRootGeometry(cx, cy, cervicalHalf, rootDepth, isUpper, kind);
        }

        var crown = BuildCrownPathFigure(
            slot.ToothNumber,
            cx,
            cy,
            cervicalHalf,
            occlusalHalf,
            crownDepth,
            baseCrownDepth,
            isUpper,
            kind);

        if (!includeRoot)
        {
            return new PathGeometry { Figures = { crown } };
        }

        var root = BuildRootGeometry(cx, cy, cervicalHalf, rootDepth, isUpper, kind);
        var combined = new PathGeometry { Figures = { crown } };
        foreach (var figure in root.Figures)
        {
            combined.Figures.Add(figure);
        }

        return combined;
    }

    private static PathFigure BuildCrownPathFigure(
        int toothNumber,
        double cx,
        double cy,
        double cervicalHalf,
        double occlusalHalf,
        double crownDepth,
        double baseCrownDepth,
        bool isUpper,
        ToothKind kind)
    {
        if (isUpper)
        {
            double yOcclusal = cy + crownDepth;
            double yCervical = cy - crownDepth;
            return BuildUpperCrown(toothNumber, cx, yCervical, yOcclusal, cervicalHalf, occlusalHalf, crownDepth, kind);
        }

        double yCervicalLower = cy + crownDepth;
        double yOcclusalLower = cy - crownDepth;
        return BuildLowerCrown(toothNumber, cx, yCervicalLower, yOcclusalLower, cervicalHalf, occlusalHalf, crownDepth, kind);
    }

    private static PathFigure BuildUpperCrown(
        int toothNumber,
        double cx,
        double yCervical,
        double yOcclusal,
        double cervicalHalf,
        double occlusalHalf,
        double crownDepth,
        ToothKind kind)
    {
        double left = cx - occlusalHalf;
        double right = cx + occlusalHalf;
        double r = CornerRadius(kind, occlusalHalf);
        bool upperAnterior = toothNumber is >= 6 and <= 11;
        double cervBulge = crownDepth * (upperAnterior ? 0.30 : 0.14);

        var figure = new PathFigure { IsClosed = true, StartPoint = new Point(cx - cervicalHalf, yCervical) };
        figure.Segments.Add(new LineSegment(new Point(left, yOcclusal - r), true));
        AppendBiteProfile(figure, toothNumber, kind, cx, left, right, yOcclusal, occlusalHalf, r, bite: 1);
        figure.Segments.Add(new LineSegment(new Point(cx + cervicalHalf, yCervical), true));
        figure.Segments.Add(new QuadraticBezierSegment(
            new Point(cx, yCervical - cervBulge),
            figure.StartPoint,
            true));
        return figure;
    }

    private static PathFigure BuildLowerCrown(
        int toothNumber,
        double cx,
        double yCervical,
        double yOcclusal,
        double cervicalHalf,
        double occlusalHalf,
        double crownDepth,
        ToothKind kind)
    {
        double left = cx - occlusalHalf;
        double right = cx + occlusalHalf;
        double r = CornerRadius(kind, occlusalHalf);
        double cervBulge = crownDepth * 0.14;

        var figure = new PathFigure { IsClosed = true, StartPoint = new Point(cx - cervicalHalf, yCervical) };
        figure.Segments.Add(new LineSegment(new Point(left, yOcclusal + r), true));
        AppendBiteProfile(figure, toothNumber, kind, cx, left, right, yOcclusal, occlusalHalf, r, bite: -1);
        figure.Segments.Add(new LineSegment(new Point(cx + cervicalHalf, yCervical), true));
        figure.Segments.Add(new QuadraticBezierSegment(
            new Point(cx, yCervical + cervBulge),
            figure.StartPoint,
            true));
        return figure;
    }

    private static double CornerRadius(ToothKind kind, double occlusalHalf) =>
        occlusalHalf * (kind switch
        {
            ToothKind.Incisor => 0.12,
            ToothKind.Canine => 0.12,
            _ => 0.26,
        });

    /// <summary>
    /// Biting-edge profile drawn left to right, starting at (left, yOcclusal - bite*r) and
    /// ending at (right, yOcclusal - bite*r). Control points at x=left/right give vertical
    /// tangents, so the edge rounds smoothly into the sides with no corner spurs.
    /// bite = +1 for upper teeth (edge extends downward), -1 for lower teeth (upward).
    /// </summary>
    private static void AppendBiteProfile(
        PathFigure figure,
        int toothNumber,
        ToothKind kind,
        double cx,
        double left,
        double right,
        double yOcclusal,
        double half,
        double r,
        double bite)
    {
        double sideY = yOcclusal - (bite * r);
        double Depth(double f) => yOcclusal + (bite * half * f);

        switch (kind)
        {
            case ToothKind.Incisor:
            {
                double d = toothNumber switch
                {
                    8 or 9 => 0.16,
                    7 or 10 => 0.20,
                    23 or 24 or 25 or 26 => 0.14,
                    _ => 0.16,
                };
                figure.Segments.Add(new BezierSegment(
                    new Point(left, Depth(d)),
                    new Point(right, Depth(d)),
                    new Point(right, sideY),
                    true));
                break;
            }

            case ToothKind.Canine:
            {
                // Pentagon cusp: nearly straight slopes meeting at a blunt point (sample style).
                bool upperCanine = toothNumber is 6 or 11;
                double tipY = Depth(upperCanine ? 0.55 : 0.42);
                figure.Segments.Add(new BezierSegment(
                    new Point(left, Depth(0.18)),
                    new Point(cx - (half * 0.45), Depth(upperCanine ? 0.46 : 0.36)),
                    new Point(cx, tipY),
                    true));
                figure.Segments.Add(new BezierSegment(
                    new Point(cx + (half * 0.45), Depth(upperCanine ? 0.46 : 0.36)),
                    new Point(right, Depth(0.18)),
                    new Point(right, sideY),
                    true));
                break;
            }

            case ToothKind.Premolar:
                figure.Segments.Add(new BezierSegment(
                    new Point(left, Depth(0.34)),
                    new Point(cx - (half * 0.42), Depth(0.34)),
                    new Point(cx, Depth(0.13)),
                    true));
                figure.Segments.Add(new BezierSegment(
                    new Point(cx + (half * 0.42), Depth(0.34)),
                    new Point(right, Depth(0.34)),
                    new Point(right, sideY),
                    true));
                break;

            default: // Molar: three soft scallops
                figure.Segments.Add(new BezierSegment(
                    new Point(left, Depth(0.30)),
                    new Point(cx - (half * 0.60), Depth(0.30)),
                    new Point(cx - (half * 0.34), Depth(0.10)),
                    true));
                figure.Segments.Add(new BezierSegment(
                    new Point(cx - (half * 0.14), Depth(0.24)),
                    new Point(cx + (half * 0.14), Depth(0.24)),
                    new Point(cx + (half * 0.34), Depth(0.10)),
                    true));
                figure.Segments.Add(new BezierSegment(
                    new Point(cx + (half * 0.60), Depth(0.30)),
                    new Point(right, Depth(0.30)),
                    new Point(right, sideY),
                    true));
                break;
        }
    }

    private static PathGeometry BuildRootGeometry(
        double cx,
        double cy,
        double cervicalHalf,
        double rootDepth,
        bool isUpper,
        ToothKind kind)
    {
        var geometry = new PathGeometry();

        if (kind == ToothKind.Molar)
        {
            foreach (var figure in BuildMolarRootFigures(cx, cy, cervicalHalf, rootDepth, isUpper))
            {
                geometry.Figures.Add(figure);
            }

            return geometry;
        }

        if (kind == ToothKind.Premolar)
        {
            geometry.Figures.Add(BuildPremolarRootFigure(cx, cy, cervicalHalf, rootDepth, isUpper));
            return geometry;
        }

        geometry.Figures.Add(BuildSingleRootFigure(cx, cy, cervicalHalf, rootDepth, isUpper));
        return geometry;
    }

    private static PathFigure BuildSingleRootFigure(
        double cx,
        double cy,
        double cervicalHalf,
        double rootDepth,
        bool isUpper)
    {
        double taper = cervicalHalf * 0.38;
        var figure = new PathFigure { IsClosed = false };

        if (isUpper)
        {
            double yCervical = cy - (rootDepth * 0.52);
            double yRoot = cy - rootDepth;
            figure.StartPoint = new Point(cx - cervicalHalf, yCervical);
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx - taper, (yCervical + yRoot) / 2),
                new Point(cx, yRoot),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx + taper, (yCervical + yRoot) / 2),
                new Point(cx + cervicalHalf, yCervical),
                true));
        }
        else
        {
            double yCervical = cy + (rootDepth * 0.52);
            double yRoot = cy + rootDepth;
            figure.StartPoint = new Point(cx - cervicalHalf, yCervical);
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx - taper, (yCervical + yRoot) / 2),
                new Point(cx, yRoot),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx + taper, (yCervical + yRoot) / 2),
                new Point(cx + cervicalHalf, yCervical),
                true));
        }

        return figure;
    }

    private static PathFigure BuildPremolarRootFigure(
        double cx,
        double cy,
        double cervicalHalf,
        double rootDepth,
        bool isUpper)
    {
        double spread = cervicalHalf * 0.34;
        double taper = cervicalHalf * 0.22;
        var figure = new PathFigure { IsClosed = false };

        if (isUpper)
        {
            double yCervical = cy - (rootDepth * 0.52);
            double yRoot = cy - rootDepth;
            figure.StartPoint = new Point(cx - cervicalHalf, yCervical);
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx - spread, (yCervical + yRoot) / 2),
                new Point(cx - spread * 0.55, yRoot),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx + spread * 0.55, yRoot),
                new Point(cx + spread, (yCervical + yRoot) / 2),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx + taper, (yCervical + yRoot) / 2),
                new Point(cx + cervicalHalf, yCervical),
                true));
        }
        else
        {
            double yCervical = cy + (rootDepth * 0.52);
            double yRoot = cy + rootDepth;
            figure.StartPoint = new Point(cx - cervicalHalf, yCervical);
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx - spread, (yCervical + yRoot) / 2),
                new Point(cx - spread * 0.55, yRoot),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx + spread * 0.55, yRoot),
                new Point(cx + spread, (yCervical + yRoot) / 2),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(cx + taper, (yCervical + yRoot) / 2),
                new Point(cx + cervicalHalf, yCervical),
                true));
        }

        return figure;
    }

    private static IEnumerable<PathFigure> BuildMolarRootFigures(
        double cx,
        double cy,
        double cervicalHalf,
        double rootDepth,
        bool isUpper)
    {
        double yCervical = isUpper ? cy - (rootDepth * 0.52) : cy + (rootDepth * 0.52);
        double yRoot = isUpper ? cy - rootDepth : cy + rootDepth;
        double midY = (yCervical + yRoot) / 2;

        if (isUpper)
        {
            yield return BuildRootBranch(cx - cervicalHalf * 0.55, yCervical, cx - cervicalHalf * 0.78, midY, cx - cervicalHalf * 0.72, yRoot);
            yield return BuildRootBranch(cx, yCervical, cx, midY, cx, yRoot - rootDepth * 0.03);
            yield return BuildRootBranch(cx + cervicalHalf * 0.55, yCervical, cx + cervicalHalf * 0.78, midY, cx + cervicalHalf * 0.72, yRoot);
            yield break;
        }

        yield return BuildRootBranch(cx - cervicalHalf * 0.42, yCervical, cx - cervicalHalf * 0.62, midY, cx - cervicalHalf * 0.58, yRoot);
        yield return BuildRootBranch(cx + cervicalHalf * 0.42, yCervical, cx + cervicalHalf * 0.62, midY, cx + cervicalHalf * 0.58, yRoot);
    }

    private static PathFigure BuildRootBranch(double xStart, double yStart, double xMid, double yMid, double xEnd, double yEnd)
    {
        var figure = new PathFigure { IsClosed = false, StartPoint = new Point(xStart, yStart) };
        figure.Segments.Add(new QuadraticBezierSegment(new Point(xMid, yMid), new Point(xEnd, yEnd), true));
        return figure;
    }

    private static void ApplyToothAdjustments(
        int toothNumber,
        ref double cervicalHalf,
        ref double occlusalHalf,
        ref double crownDepth)
    {
        switch (toothNumber)
        {
            case 8 or 9:
                occlusalHalf *= 1.02;
                crownDepth *= 1.12;
                break;
            case 6 or 11:
                occlusalHalf *= 0.98;
                cervicalHalf *= 0.92;
                crownDepth *= 1.10;
                break;
            case 7 or 10:
                occlusalHalf *= 0.86;
                cervicalHalf *= 0.85;
                crownDepth *= 1.05;
                break;
            case 22 or 27:
                occlusalHalf *= 1.06;
                crownDepth *= 1.02;
                break;
            case 23 or 24 or 25 or 26:
                occlusalHalf *= 0.88;
                cervicalHalf *= 0.90;
                break;
        }
    }

    private static ToothKind ResolveKind(int toothNumber) =>
        toothNumber switch
        {
            1 or 2 or 3 or 14 or 15 or 16 or 17 or 18 or 19 or 30 or 31 or 32 => ToothKind.Molar,
            4 or 5 or 12 or 13 or 20 or 21 or 28 or 29 => ToothKind.Premolar,
            6 or 11 or 22 or 27 => ToothKind.Canine,
            _ => ToothKind.Incisor,
        };
}
