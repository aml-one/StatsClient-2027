import re
from pathlib import Path

path = Path(__file__).resolve().parent.parent / "StatsClient" / "Themes" / "ColorSchemes" / "ListViewItemColors.Light.xaml"
text = path.read_text(encoding="utf-8")
replacements = {
    "Transparent": "TransparentColor_Cl",
    "White": "WindowBackgroundColor",
    "Black": "BlackColor_Cl",
}
count = 0
for name, color_key in replacements.items():
    pattern = rf'<SolidColorBrush x:Key="([^"]+)">{name}</SolidColorBrush>'
    text, n = re.subn(
        pattern,
        rf'<SolidColorBrush x:Key="\1" Color="{{StaticResource {color_key}}}" />',
        text,
    )
    count += n
path.write_text(text, encoding="utf-8")
print(f"ListViewItemColors replacements: {count}")
