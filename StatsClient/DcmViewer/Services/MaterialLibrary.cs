using System.Collections.Generic;
using System.Windows.Media;

namespace DCMViewer.Services;

/// <summary>
/// Colors for a single named material/texture entry.
/// </summary>
public readonly record struct MaterialPalette(Color FrontDiffuse, Color BackDiffuse, Color FrontSpecular);

/// <summary>
/// Named material library.  Add entries here to extend the texture list.
/// "Model"    - neutral gray for scan/jaw models (default).
/// "Zirconia" - warm ivory for tooth/restoration meshes.
/// "Emax"     - cool blueish porcelain look.
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
        "Stone",
        "Gold",
        "SLM",
        "PMMA",
        "WAX"
    };

    private static readonly Dictionary<string, MaterialPalette> _palettes =
        new(StringComparer.OrdinalIgnoreCase)
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
                Color.FromRgb(168, 174, 212),
                Color.FromRgb(130, 136, 180),
                Color.FromRgb(200, 208, 240)),

            ["Stone"] = new MaterialPalette(
                Color.FromRgb(235, 210, 120),
                Color.FromRgb(200, 170, 90),
                Color.FromRgb(210, 190, 140)),

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

    /// <summary>Ordered list of available texture names (for ComboBox binding).</summary>
    public static IReadOnlyList<string> Names => _names;

    /// <summary>Returns the palette for <paramref name="name"/>, or the default palette if not found.</summary>
    public static MaterialPalette Get(string name) =>
        _palettes.TryGetValue(name, out var p) ? p : _palettes[DefaultName];

    /// <summary>Returns true if the library contains an entry with this name.</summary>
    public static bool Contains(string name) =>
        !string.IsNullOrWhiteSpace(name) && _palettes.ContainsKey(name);
}
