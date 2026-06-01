#!/usr/bin/env python3
"""Migrate hardcoded WPF named colors into ColorScheme resource references."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient"

EXCLUDE_DIRS = {"bin", "obj"}
EXCLUDE_FILES = {"ClassicColorScheme.xaml", "ModernColorScheme.xaml"}

# WPF named color -> scheme resource key
BRUSH_ALIASES: dict[str, str] = {
    "Transparent": "TransparentBrush",
    "White": "WhiteBackground",
    "Black": "BlackColor",
    "Red": "RedColor",
    "Green": "GreenColor",
    "Blue": "BlueColor",
    "Yellow": "NamedYellow",
    "Orange": "NamedOrange",
    "Gray": "NamedGray",
    "Grey": "NamedGray",
    "Silver": "NamedSilver",
    "Brown": "NamedBrown",
    "Beige": "NamedBeige",
    "LightBlue": "NamedLightBlue",
    "LightGreen": "NamedLightGreen",
    "LightYellow": "NamedLightYellow",
    "LightSteelBlue": "NamedLightSteelBlue",
    "DeepPink": "NamedDeepPink",
    "DarkGreen": "NamedDarkGreen",
    "DarkViolet": "NamedDarkViolet",
    "DarkRed": "NamedDarkRed",
    "WhiteSmoke": "NamedWhiteSmoke",
    "DimGray": "NamedDimGray",
    "Pink": "NamedPink",
    "LightCoral": "NamedLightCoral",
    "BlanchedAlmond": "LightTextColor",
    "SteelBlue": "AgeColorSteel",
    "IndianRed": "DialogCloseButtonBackground",
    "SeaGreen": "AgeColorFresh",
    "LimeGreen": "AgeColorRecent",
    "Maroon": "AgeColorMaroon",
    "YellowGreen": "OrderByColorFresh",
    "Purple": "PurpleColor",
    "LightPink": "LightPinkColor",
    "LightGreenColor": "LightGreenColor",
}

COLOR_PROPS = {
    "Background",
    "Foreground",
    "BorderBrush",
    "Fill",
    "Stroke",
    "CaretBrush",
    "Color",
}

TRIGGER_CONTEXT_RE = re.compile(
    r"(<(?:Trigger|DataTrigger|MultiTrigger|MultiDataTrigger)[^>]*>[\s\S]*?</(?:Trigger|DataTrigger|MultiTrigger|MultiDataTrigger)>)",
    re.IGNORECASE,
)

# Match named color values on brush/color properties (not inside DynamicResource already)
PROP_NAMED_RE = re.compile(
    r"(Background|Foreground|BorderBrush|Fill|Stroke|CaretBrush|Color|"
    r"d:Background|d:Foreground|d:BorderBrush|d:Fill|d:Stroke)="
    r'(["\'])({})\2'.format("|".join(re.escape(n) for n in sorted(BRUSH_ALIASES, key=len, reverse=True))),
    re.IGNORECASE,
)

VALUE_NAMED_RE = re.compile(
    r'Value="({})"'.format("|".join(re.escape(n) for n in sorted(BRUSH_ALIASES, key=len, reverse=True))),
    re.IGNORECASE,
)


def is_theme_file(path: Path) -> bool:
    return "Themes" in path.parts and path.suffix.lower() == ".xaml"


def is_scheme_file(path: Path) -> bool:
    return "ColorSchemes" in path.parts


def resource_markup(key: str, use_static: bool, prop: str) -> str:
    kind = "StaticResource" if use_static else "DynamicResource"
    if prop == "Color":
        color_key = key if key.endswith("_Cl") else f"{key}_Cl"
        if key == "BlackColor":
            color_key = "BlackColor_Cl"
        elif key == "TransparentBrush":
            color_key = "BlackColor_Cl"  # fallback; transparent shadows use black at 0 opacity in effects
        return f"{{{kind} {color_key}}}"
    return f"{{{kind} {key}}}"


def replace_in_segment(segment: str, path: Path, in_trigger: bool) -> tuple[str, int]:
    use_static = is_theme_file(path) or is_scheme_file(path)
    static = use_static or in_trigger
    count = 0

    def repl_prop(m: re.Match) -> str:
        nonlocal count
        prop, quote, name = m.group(1), m.group(2), m.group(3)
        key = BRUSH_ALIASES.get(name) or BRUSH_ALIASES.get(name.title()) or BRUSH_ALIASES.get(
            name[0].upper() + name[1:].lower()
        )
        if not key:
            return m.group(0)
        # Transparent on DropShadowEffect Color - use BlackColor_Cl (standard shadow)
        if prop == "Color" and name.lower() == "transparent":
            key = "BlackColor_Cl"
        count += 1
        return f'{prop}={quote}{resource_markup(key, static, prop)}{quote}'

    segment = PROP_NAMED_RE.sub(repl_prop, segment)

    def repl_value(m: re.Match) -> str:
        nonlocal count
        name = m.group(1)
        key = BRUSH_ALIASES.get(name) or BRUSH_ALIASES.get(name.title())
        if not key:
            return m.group(0)
        count += 1
        return f'Value="{resource_markup(key, static, "Background")}"'

    segment = VALUE_NAMED_RE.sub(repl_value, segment)
    return segment, count


def replace_xaml(content: str, path: Path) -> tuple[str, int]:
    total = 0
    parts: list[tuple[str, bool]] = []
    last = 0
    for m in TRIGGER_CONTEXT_RE.finditer(content):
        if m.start() > last:
            parts.append((content[last : m.start()], False))
        parts.append((m.group(0), True))
        last = m.end()
    if last < len(content):
        parts.append((content[last:], False))
    if not parts:
        parts = [(content, False)]

    out = []
    for seg, trig in parts:
        replaced, n = replace_in_segment(seg, path, trig)
        out.append(replaced)
        total += n
    return "".join(out), total


def replace_cs_brushes(content: str) -> tuple[str, int]:
    mapping = {
        "Brushes.Transparent": 'ColorSchemeResourceCatalog.GetBrush("TransparentBrush")',
        "Brushes.White": 'ColorSchemeResourceCatalog.GetBrush("WhiteBackground")',
        "Brushes.Black": 'ColorSchemeResourceCatalog.GetBrush("BlackColor")',
        "Brushes.Red": 'ColorSchemeResourceCatalog.GetBrush("RedColor")',
        "Brushes.Green": 'ColorSchemeResourceCatalog.GetBrush("GreenColor")',
        "Brushes.Blue": 'ColorSchemeResourceCatalog.GetBrush("BlueColor")',
        "Brushes.Yellow": 'ColorSchemeResourceCatalog.GetBrush("NamedYellow")',
        "Brushes.Gray": 'ColorSchemeResourceCatalog.GetBrush("NamedGray")',
        "Brushes.Grey": 'ColorSchemeResourceCatalog.GetBrush("NamedGray")',
        "Brushes.Silver": 'ColorSchemeResourceCatalog.GetBrush("NamedSilver")',
        "Brushes.SteelBlue": 'ColorSchemeResourceCatalog.GetBrush("AgeColorSteel")',
        "Brushes.DimGray": 'ColorSchemeResourceCatalog.GetBrush("NamedDimGray")',
        "Brushes.WhiteSmoke": 'ColorSchemeResourceCatalog.GetBrush("NamedWhiteSmoke")',
        "Brushes.Orange": 'ColorSchemeResourceCatalog.GetBrush("NamedOrange")',
        "Brushes.LightBlue": 'ColorSchemeResourceCatalog.GetBrush("NamedLightBlue")',
        "Brushes.LightGreen": 'ColorSchemeResourceCatalog.GetBrush("NamedLightGreen")',
        "Brushes.LightYellow": 'ColorSchemeResourceCatalog.GetBrush("NamedLightYellow")',
        "Brushes.Pink": 'ColorSchemeResourceCatalog.GetBrush("NamedPink")',
        "Brushes.Brown": 'ColorSchemeResourceCatalog.GetBrush("NamedBrown")',
        "Brushes.Beige": 'ColorSchemeResourceCatalog.GetBrush("NamedBeige")',
        "Brushes.LightCoral": 'ColorSchemeResourceCatalog.GetBrush("NamedLightCoral")',
        "Brushes.DeepPink": 'ColorSchemeResourceCatalog.GetBrush("NamedDeepPink")',
        "Brushes.DarkGreen": 'ColorSchemeResourceCatalog.GetBrush("NamedDarkGreen")',
        "Brushes.DarkViolet": 'ColorSchemeResourceCatalog.GetBrush("NamedDarkViolet")',
        "Brushes.DarkRed": 'ColorSchemeResourceCatalog.GetBrush("NamedDarkRed")',
        "Brushes.IndianRed": 'ColorSchemeResourceCatalog.GetBrush("DialogCloseButtonBackground")',
    }
    count = 0
    out = content
    for old, new in sorted(mapping.items(), key=lambda x: -len(x[0])):
        n = out.count(old)
        if n:
            out = out.replace(old, new)
            count += n
    return out, count


def replace_cs_strings(content: str) -> tuple[str, int]:
    string_map = {
        '"Black"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Black")',
        '"White"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_White")',
        '"Red"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Red")',
        '"Yellow"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Yellow")',
        '"Green"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Green")',
        '"Gray"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Gray")',
        '"Blue"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Blue")',
        '"Transparent"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Transparent")',
        '"White"': 'ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_White")',
        "Background = \"White\"": 'Background = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_White")',
    }
    count = 0
    out = content
    # Specific patterns first
    patterns = [
        (r'foreColor = "Yellow"', 'foreColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Yellow")'),
        (r'PanColor = "Transparent"', 'PanColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Transparent")'),
        (r'panColor = "Transparent"', 'panColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Transparent")'),
        (r'== "Transparent"', '== ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Transparent")'),
        (r'LineColor { get; set; } = "Black"', 'LineColor { get; set; } = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Black")'),
        (r'eventColor = "Black"', 'eventColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Black")'),
        (r'panNrDuplicatesFontColor = "Red"', 'panNrDuplicatesFontColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Red")'),
        (r'PcPanColor = "Black"', 'PcPanColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Black")'),
        (r'PanNrDuplicatesFontColor == "Red"', 'PanNrDuplicatesFontColor == ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Red")'),
        (r'PanNrDuplicatesFontColor = "Yellow"', 'PanNrDuplicatesFontColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Yellow")'),
        (r'PanNrDuplicatesFontColor = "Red"', 'PanNrDuplicatesFontColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Red")'),
        (r'model.LineColor = "Yellow"', 'model.LineColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Yellow")'),
        (r'model.CommentColor = "Gray"', 'model.CommentColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Gray")'),
        (r'model.CommentColor = "Blue"', 'model.CommentColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Blue")'),
        (r'PanColorName = "Green"', 'PanColorName = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Green")'),
        (r'AddEventToEventListLocalDB\([^,]+, "Yellow"\)', None),  # handled separately
    ]
    import re as re_mod
    for pat, repl in patterns:
        if repl is None:
            continue
        new_out, n = re_mod.subn(pat, repl, out)
        if n:
            out = new_out
            count += n

    event_replacements = [
        ('AddEventToEventListLocalDB($"Grabbed a pan number: {LastUsedPanNumber}", "Yellow")',
         'AddEventToEventListLocalDB($"Grabbed a pan number: {LastUsedPanNumber}", ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Yellow"))'),
        ('AddEventToEventListLocalDB($"Image was not saved! ({NextPanNumber})", "Red")',
         'AddEventToEventListLocalDB($"Image was not saved! ({NextPanNumber})", ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Red"))'),
        ('AddEventToEventListLocalDB($"Prescription image successfully saved: {NextPanNumber}", "Green")',
         'AddEventToEventListLocalDB($"Prescription image successfully saved: {NextPanNumber}", ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Green"))'),
    ]
    for old, new in event_replacements:
        if old in out:
            out = out.replace(old, new)
            count += 1

    return out, count


def iter_files() -> list[Path]:
    files = []
    for path in ROOT.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix.lower() not in {".xaml", ".cs"}:
            continue
        if any(p in EXCLUDE_DIRS for p in path.parts):
            continue
        if path.name in EXCLUDE_FILES:
            continue
        if "ClassicColorScheme" in str(path) or "ModernColorScheme" in str(path):
            continue
        files.append(path)
    return files


def main() -> None:
    total = 0
    for path in iter_files():
        text = path.read_text(encoding="utf-8", errors="replace")
        updated = text
        n = 0
        if path.suffix.lower() == ".xaml":
            updated, n = replace_xaml(text, path)
        elif path.suffix.lower() == ".cs":
            updated, nb = replace_cs_brushes(text)
            updated, ns = replace_cs_strings(updated)
            n = nb + ns
        if updated != text:
            path.write_text(updated, encoding="utf-8")
            print(f"Updated {path.relative_to(ROOT)} ({n} refs)")
            total += n
    print(f"Total named-color replacements: {total}")


if __name__ == "__main__":
    main()
