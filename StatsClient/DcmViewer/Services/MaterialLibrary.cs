using System.Collections.Generic;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace DCMViewer.Services;

/// <summary>
/// Colors and optional Phong tuning for a named material/texture entry.
/// </summary>
/// <param name="SpecularShininessOverride">When &gt; 0, replaces the global satin/matte shininess.</param>
/// <param name="SpecularIntensityScale">Multiplier on the global specular intensity.</param>
/// <param name="AmbientScale">Diffuse ambient multiplier.</param>
/// <param name="EmissiveScale">Back-diffuse emissive multiplier.</param>
public readonly record struct MaterialPalette(
    Color FrontDiffuse,
    Color BackDiffuse,
    Color FrontSpecular,
    double SpecularShininessOverride = 0,
    double SpecularIntensityScale = 1.0,
    double AmbientScale = 0.25,
    double EmissiveScale = 0.15);

/// <summary>
/// Named material library.  Add entries here to extend the texture list.
/// "Model"    - neutral gray for scan/jaw models (default).
/// "Zirconia" - warm ivory for tooth/restoration meshes.
/// "Emax"     - blueish translucent ceramic / glass look.
/// "Preop"    - 3Shape-style light periwinkle for pre-prep scans.
/// "Stone"    - stone model look.
/// "Gold"     - gold metal look.
/// "SLM"      - silver metal look.
/// "PMMA"     - yellowish plastic look.
/// "WAX"      - white matte wax look.
/// </summary>
public static class MaterialLibrary
{
    public const string DefaultName = "Model";

    // Ordered list used for UI ComboBox display.
    private static readonly string[] _names =
    {
        "Model",
        "Zirconia",
        "Emax",
        "Preop",
        "Stone",
        "Gold",
        "SLM",
        "PMMA",
        "WAX"
    };

    private static Color ResolveColor(string key, byte fallbackR, byte fallbackG, byte fallbackB) =>
        ColorSchemeResourceCatalog.TryGetColor(key, out var color)
            ? color
            : Color.FromRgb(fallbackR, fallbackG, fallbackB);

    private static Color Darken(Color color, double factor) =>
        Color.FromRgb(
            (byte)(color.R * factor),
            (byte)(color.G * factor),
            (byte)(color.B * factor));

    private static Color Lighten(Color color, double factor) =>
        Color.FromRgb(
            (byte)Math.Min(255, color.R + (255 - color.R) * factor),
            (byte)Math.Min(255, color.G + (255 - color.G) * factor),
            (byte)Math.Min(255, color.B + (255 - color.B) * factor));

    private static Dictionary<string, MaterialPalette> BuildPalettes()
    {
        var preopBase = ResolveColor("ViewerMaterialPreopColor", 165, 184, 232);
        return new Dictionary<string, MaterialPalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["Model"]    = new MaterialPalette(
                Color.FromRgb(202, 202, 202),
                Color.FromRgb(150, 150, 150),
                Color.FromRgb(146, 146, 146)),

            ["Zirconia"] = new MaterialPalette(
                Color.FromRgb(224, 219, 197),
                Color.FromRgb(192, 184, 165),
                Color.FromRgb(154, 148, 128)),

            ["Emax"] = new MaterialPalette(
                FrontDiffuse: Color.FromRgb(178, 198, 238),
                BackDiffuse: Color.FromRgb(118, 142, 192),
                FrontSpecular: Color.FromRgb(245, 250, 255),
                SpecularShininessOverride: 142,
                SpecularIntensityScale: 1.18,
                AmbientScale: 0.18,
                EmissiveScale: 0.06),

            ["Preop"] = new MaterialPalette(
                FrontDiffuse: preopBase,
                BackDiffuse: Darken(preopBase, 0.76),
                FrontSpecular: Lighten(preopBase, 0.35),
                SpecularShininessOverride: 78,
                SpecularIntensityScale: 0.92,
                AmbientScale: 0.24,
                EmissiveScale: 0.12),

            ["Stone"] = new MaterialPalette(
                Color.FromRgb(235, 210, 120),
                Color.FromRgb(200, 170, 90),
                Color.FromRgb(210, 190, 140)),

            // Temporary preview while picking unified-shell working side (not listed in UI).
            ["WorkingSide"] = new MaterialPalette(
                FrontDiffuse: Color.FromRgb(168, 210, 152),
                BackDiffuse: Color.FromRgb(118, 158, 104),
                FrontSpecular: Color.FromRgb(205, 238, 188),
                SpecularShininessOverride: 78,
                SpecularIntensityScale: 1.05,
                AmbientScale: 0.24,
                EmissiveScale: 0.1),

            ["Gold"] = new MaterialPalette(
                Color.FromRgb(230, 190, 58),
                Color.FromRgb(168, 129, 32),
                Color.FromRgb(255, 235, 150)),

            ["SLM"] = new MaterialPalette(
                Color.FromRgb(198, 204, 213),
                Color.FromRgb(126, 133, 143),
                Color.FromRgb(242, 246, 252)),

            ["PMMA"] = new MaterialPalette(
                Color.FromRgb(239, 223, 160),
                Color.FromRgb(203, 186, 126),
                Color.FromRgb(172, 159, 116)),

            ["WAX"] = new MaterialPalette(
                Color.FromRgb(255, 255, 252),
                Color.FromRgb(252, 252, 248),
                Color.FromRgb(220, 220, 210)),
        };
    }

    private static readonly Lazy<Dictionary<string, MaterialPalette>> _palettes =
        new(BuildPalettes);

    /// <summary>Ordered list of available texture names (for ComboBox binding).</summary>
    public static IReadOnlyList<string> Names => _names;

    /// <summary>Returns the palette for <paramref name="name"/>, or the default palette if not found.</summary>
    public static MaterialPalette Get(string name) =>
        _palettes.Value.TryGetValue(name, out var p) ? p : _palettes.Value[DefaultName];

    /// <summary>Returns true if the library contains an entry with this name.</summary>
    public static bool Contains(string name) =>
        !string.IsNullOrWhiteSpace(name) && _palettes.Value.ContainsKey(name);
}
