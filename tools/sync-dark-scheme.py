from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient" / "Themes" / "ColorSchemes"
LIGHT = ROOT / "Light.xaml"
DARK = ROOT / "Dark.xaml"
LV_LIGHT = ROOT / "ListViewItemColors.Light.xaml"
LV_DARK = ROOT / "ListViewItemColors.Dark.xaml"

# Dark.xaml has a custom dark window/splash palette. Only ListView item colors are synced
# from Light so keys stay aligned. Edit Dark.xaml window chrome manually when needed.

lv_light = LV_LIGHT.read_text(encoding="utf-8")
lv_dark = lv_light.replace(
    '<sys:String x:Key="ColorSchemeName">Light</sys:String>',
    '<sys:String x:Key="ColorSchemeName">Dark</sys:String>',
).replace(
    "Per-ListView and per-ListViewItem colors (Light).",
    "Per-ListView and per-ListViewItem colors (Dark).",
)
LV_DARK.write_text(lv_dark, encoding="utf-8")

# Keep Dark.xaml keys aligned with Light (non-window tokens) without overwriting window/splash palette.
if DARK.exists():
    dark_text = DARK.read_text(encoding="utf-8")
    if 'ColorSchemeName">Dark<' not in dark_text:
        print("WARNING: Dark.xaml ColorSchemeName is not Dark — review manually.")
else:
    light = LIGHT.read_text(encoding="utf-8")
    dark = light.replace(
        '<sys:String x:Key="ColorSchemeName">Light</sys:String>',
        '<sys:String x:Key="ColorSchemeName">Dark</sys:String>',
    ).replace("ListViewItemColors.Light.xaml", "ListViewItemColors.Dark.xaml").replace(
        "<!-- StatsClient Color Scheme: Light",
        "<!-- StatsClient Color Scheme: Dark",
    )
    DARK.write_text(dark, encoding="utf-8")
    print("Created Dark.xaml from Light (first-time bootstrap only).")

print("Synced ListViewItemColors.Dark.xaml from Light (Dark.xaml window palette preserved).")
