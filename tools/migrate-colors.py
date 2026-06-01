#!/usr/bin/env python3
"""Migrate hardcoded hex colors into ColorSchemes/Light.xaml and wire references."""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient"
LIGHT = ROOT / "Themes" / "ColorSchemes" / "Light.xaml"
DARK = ROOT / "Themes" / "ColorSchemes" / "Dark.xaml"
LV_LIGHT = ROOT / "Themes" / "ColorSchemes" / "ListViewItemColors.Light.xaml"
LV_DARK = ROOT / "Themes" / "ColorSchemes" / "ListViewItemColors.Dark.xaml"

EXCLUDE_DIRS = {"bin", "obj", "ColorSchemes"}
EXCLUDE_FILES = {
    "ClassicColorScheme.xaml",
    "ModernColorScheme.xaml",
}

# Known hex -> existing scheme key (brush keys unless noted with _Cl)
ALIASES: dict[str, str] = {
    "#FF334155": "ImmersiveTextPrimary",
    "#334155": "ImmersiveTextPrimary",
    "#FF475569": "ImmersiveTextSecondary",
    "#475569": "ImmersiveTextSecondary",
    "#FF64748B": "ImmersiveTextMuted",
    "#64748B": "ImmersiveTextMuted",
    "#FF1E293B": "ImmersiveFilterButtonForeground",
    "#1E293B": "ImmersiveFilterButtonForeground",
    "#FF0369A1": "ImmersiveAccentBlue",
    "#0369A1": "ImmersiveAccentBlue",
    "#FFE0F2FE": "ImmersiveAccentBlueLight",
    "#E0F2FE": "ImmersiveAccentBlueLight",
    "#FFBAE6FD": "ImmersiveAccentBlueBorder",
    "#BAE6FD": "ImmersiveAccentBlueBorder",
    "#FFCBD5E1": "ImmersiveInputBorder",
    "#CBD5E1": "ImmersiveInputBorder",
    "#44CBD5E1": "ImmersiveGlassBorder",
    "#FAFFFFFF": "ImmersiveInputBackground",
    "#F8FFFFFF": "ImmersiveGlassBackground",
    "#FFFFFFFF": "WhiteBackground",
    "#FFFFFF": "WhiteBackground",
    "#FFF": "WhiteBackground",
    "#5534D9F5": "ImmersiveCardShadow_Cl",
    "#8834D9F5": "ImmersiveFocusRing_Cl",
    "#4434D9F5": "ImmersiveFilterButtonGlow",
    "#2234D9F5": "ImmersiveFilterButtonGlow",
    "#22000000": "ImmersiveFilterButtonBorder",
    "#FFF5F8FC": "ImmersiveGradientStart_Cl",
    "#FFEEF6F3": "ImmersiveGradientMid_Cl",
    "#FFF0EFF8": "ImmersiveGradientEnd_Cl",
    "#FFB8E8F5": "ImmersiveOrbCyan_Cl",
    "#00B8E8F5": "ImmersiveOrbCyanTransparent_Cl",
    "#FFFFD6E8": "ImmersiveOrbPeach_Cl",
    "#00FFD6E8": "ImmersiveOrbPeachTransparent_Cl",
    "#FFD4F5C8": "ImmersiveOrbMint_Cl",
    "#00D4F5C8": "ImmersiveOrbMintTransparent_Cl",
    "#FFE8D4F8": "ImmersiveOrbLavender_Cl",
    "#00E8D4F8": "ImmersiveOrbLavenderTransparent_Cl",
    "#FF059669": "ImmersiveSuccessText",
    "#FF0FAEE4": "ImmersiveStatusCyan",
    "#FFE40F0F": "ImmersiveStatusRed",
    "#9922FC": "ImmersivePendingDigiAccent",
    "#FF059D8A": "ImmersiveScanTeal",
    "#FF34D9F5": "AccentColor",
    "#34D9F5": "AccentColor",
    "#2C3E50": "FilterButtonForeground",
    "#EBF5FB": "FilterButtonHoverBackground",
    "#D6EAF8": "FilterButtonPressedBackground",
    "#FFD6EAF8": "FilterButtonPressedBackground",
    "#CCFFFFFF": "FilterSectionCardBackground",
    "#EFE7C9": "CreamYellowFieldColor",
    "#FFEFE7C9": "CreamYellowFieldColor",
    "#888888": "LightGrayBorderAndShadowColor",
    "#888": "LightGrayBorderAndShadowColor",
    "#444444": "DarkGrayBorderAndShadowColor",
    "#444": "DarkGrayBorderAndShadowColor",
    "#222222": "OrderInfoDividerDark",
    "#222": "OrderInfoDividerDark",
    "#666666": "OrderInfoFooterBackground",
    "#666": "OrderInfoFooterBackground",
    "#777777": "EmptyPansBackground",
    "#777": "EmptyPansBackground",
    "#999999": "EmptyPansBorder",
    "#999": "EmptyPansBorder",
    "#333333": "MessageBoxSubtitleForeground",
    "#333": "MessageBoxSubtitleForeground",
    "#000000": "BlackColor",
    "#000": "BlackColor",
    "#EEEEEE": "WindowBackgroundColor",
    "#EEE": "WindowBackgroundColor",
    "#DDDDDD": "VeryLightGrayColor",
    "#DDD": "VeryLightGrayColor",
    "#BBBBBB": "EslScrollbarThumb",
    "#BBB": "EslScrollbarThumb",
    "#CCCCCC": "OrderInfoDivider",
    "#CCC": "OrderInfoDivider",
    "#FFC9BF97": "ClassicWindowBackgroundColor",
    "#C9BF97": "ClassicWindowBackgroundColor",
    "#c9bf97": "ClassicWindowBackgroundColor",
    "#CCFFCC": "LightGreenColor",
    "#CCCCFF": "LightPrpleColor",
    "#CCFFFF": "LightPrpleColor",
    "#aa000000": "BlackSeeThruColor",
    "#66696F": "SettingsTabHeaderBackground",
    "#E8E8E8": "VeryLightGrayColor",
    "#F1DC86": "TabShapeHighlightBackground",
    "#46494F": "VitaButtonBackgroundDark",
    "#CCC": "VitaButtonBorderLight",
    "#666": "VitaButtonForegroundMid",
    "#e3bd22": "VitaButtonAccentGold",
    "#E3BD22": "VitaButtonAccentGold",
    "#86898F": "VitaButtonHoverGray",
    "#A6A98F": "VitaButtonHoverGrayAlt",
    "#FFe8c94f": "VitaButtonBorderGold",
    "#FFE8C94F": "VitaButtonBorderGold",
    "#F7DD74": "VitaButtonBackgroundYellow",
    "#FEED74": "VitaButtonBackgroundYellowAlt",
    "#FDF979": "VitaButtonBackgroundYellowBright",
    "#FFFF94": "VitaButtonBackgroundYellowLight",
    "#B7FCE2": "VitaButtonBackgroundMint",
    "#D7FCE2": "VitaButtonBackgroundMintAlt",
    "#F7FCE2": "VitaButtonBackgroundMintLight",
    "#F7ED74": "VitaButtonBackgroundYellowGreen",
    "#F7FD74": "VitaButtonBackgroundYellowPale",
    "#D7DD74": "VitaButtonBackgroundYellowDull",
    "#D7ED74": "VitaButtonBackgroundYellowSoft",
    "#D7FD74": "VitaButtonBackgroundYellowFade",
    "#F7CDD4": "VitaButtonBackgroundPink",
    "#D7DDD4": "VitaButtonBackgroundGrayGreen",
    "#C2DDD4": "VitaButtonBackgroundGrayGreenAlt",
    "#B2DDD4": "VitaButtonBackgroundGrayGreenDark",
    "#77DDD4": "VitaButtonBackgroundGrayGreenDeep",
    "#AB4505": "VitaButtonBackgroundOrange",
    "#E1CE81": "ComboBoxToggleBackground",
    "#FF97A0A5": "ComboBoxToggleBorder",
    "#DCC15B": "ComboBoxArrowBackground",
    "#FFF7FBFF": "ImmersiveFilterButtonHoverGradientStart",
    "#FFE8F4FF": "ImmersiveFilterButtonHoverGradientEnd",
    "#AAAAAA": "TabLegacyForegroundNormal",
    "#AAA": "TabLegacyForegroundHover",
    "#CCCCCC": "TabLegacyForegroundSelectedAlt",
    "#777": "TabLegacyForegroundDisabled",
    "#0A0C0D": "CircleCheckBoxBackground",
    "#FF474E51": "CircleCheckBoxBorderOuter",
    "#FF737A7D": "CircleCheckBoxBorderInner",
    "#6A6E71": "CircleCheckBoxGlyph",
    "#858C8F": "CircleCheckBoxCheckFill",
    "#EEFEFCDD": "CircleCheckBoxCheckGlow",
    "#FEFCDD": "CircleCheckBoxCheckGlow_Cl",
    "#FF333333": "ExpanderHeaderForeground",
    "#FF5593FF": "ExpanderToggleForeground",
    "#FFF3F9FF": "ExpanderContentBackground",
    "#FF3C77DD": "ExpanderHoverBorder",
    "#FFD9ECFF": "ExpanderHoverBackground",
    "#FFBCBCBC": "ExpanderDisabledForeground",
    "#FFE6E6E6": "ExpanderDisabledBackground",
    "#FF707070": "ExpanderDisabledBorder",
    "#e8d589": "ComboBoxToggleBackground",
    "#e8c94f": "VitaButtonBorderGold",
    "#27AE60": "PaymentStatusPaidColor",
    "#E74C3C": "PaymentStatusUnpaidColor",
    "#00571C": "InconsistencyBothFilledColor",
    "#06A2B0": "InconsistencyFlaggedColor",
    "#E1005A": "InconsistencyEmptyFirstColor",
    "#B07F0B": "InconsistencyEmptySecondColor",
    "#E0F2FE": "ImmersiveAccentBlueLight",
    "#B0C4DE": "ViewerSliderThumbFill",
    "#FFB8A35C": "ViewerWatermarkText",
    "#FF1F6FEB": "ViewerMeasurementColor",
    "#FF15459F": "ViewerMeasurementColorDark",
    "#00000000": "ViewerTransparentBackground",
    "#A5B8E8": "ViewerMaterialDefaultColor",
    "#46596F": "AccountInfoDefaultColor",
}

HEX_RE = re.compile(r"#(?:[0-9A-Fa-f]{3,4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})\b")

COLOR_PROPS = {
    "Color",
    "Background",
    "Foreground",
    "BorderBrush",
    "Fill",
    "Stroke",
    "CaretBrush",
}

TRIGGER_CONTEXT_RE = re.compile(
    r"(<(?:Trigger|DataTrigger|MultiTrigger|MultiDataTrigger)[^>]*>[\s\S]*?</(?:Trigger|DataTrigger|MultiTrigger|MultiDataTrigger)>)",
    re.IGNORECASE,
)


def normalize_hex(raw: str) -> str:
    h = raw.upper()
    if len(h) == 4:  # #RGB
        return f"#{h[1]}{h[1]}{h[2]}{h[2]}{h[3]}{h[3]}"
    if len(h) == 5:  # #ARGB short - expand
        a, r, g, b = h[1], h[2], h[3], h[4]
        return f"#{a}{a}{r}{r}{g}{g}{b}{b}"
    return h


def hex_to_key(hex_norm: str) -> str:
    if hex_norm in ALIASES:
        return ALIASES[hex_norm]
    body = hex_norm[1:]
    if len(body) == 6:
        return f"SchemeColor_{body}"
    if len(body) == 8:
        return f"SchemeColor_{body}"
    return f"SchemeColor_{body}"


def read_text(path: Path) -> str:
    for encoding in ("utf-8", "utf-8-sig", "cp1252", "latin-1"):
        try:
            return path.read_text(encoding=encoding)
        except UnicodeDecodeError:
            continue
    return path.read_text(encoding="utf-8", errors="replace")


def write_text(path: Path, content: str) -> None:
    path.write_text(content, encoding="utf-8")


def parse_light_keys(text: str) -> dict[str, str]:
    """Map normalized hex -> resource key from Light.xaml."""
    mapping: dict[str, str] = {}
    for m in re.finditer(
        r'<(?:SolidColorBrush|Color)\s+x:Key="([^"]+)"(?:\s+Color="([^"]+)"|>([^<]+)<)',
        text,
    ):
        key, color_attr, color_inner = m.group(1), m.group(2), m.group(3)
        val = color_attr or color_inner
        if val and val.startswith("#"):
            mapping[normalize_hex(val)] = key
    return mapping


def should_process_cs(path: Path) -> bool:
    rel = path.relative_to(ROOT).as_posix()
    if rel.startswith("MVVM/Converters/"):
        return True
    if rel.startswith("DcmViewer/"):
        return True
    if rel == "MVVM/Core/ColorSchemeManager.cs":
        return True
    if rel == "MVVM/Model/AccountInfoModel.cs":
        return True
    if rel.startswith("MVVM/ViewModel/"):
        return True
    return False


def iter_source_files() -> list[Path]:
    files: list[Path] = []
    for path in ROOT.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix.lower() not in {".xaml", ".cs"}:
            continue
        if any(part in EXCLUDE_DIRS for part in path.parts):
            continue
        if path.name in EXCLUDE_FILES:
            continue
        if "ColorSchemes" in path.parts:
            continue
        files.append(path)
    return files


def is_theme_file(path: Path) -> bool:
    return "Themes" in path.parts and path.suffix.lower() == ".xaml"


def resource_markup(key: str, use_static: bool) -> str:
    kind = "StaticResource" if use_static else "DynamicResource"
    return f"{{{kind} {key}}}"


def resource_markup_color(key: str, use_static: bool) -> str:
    kind = "StaticResource" if use_static else "DynamicResource"
    color_key = key if key.endswith("_Cl") else f"{key}_Cl"
    return f"{{{kind} {color_key}}}"


def replace_hex_in_xaml(content: str, path: Path, hex_to_res: dict[str, str]) -> tuple[str, int]:
    use_static = is_theme_file(path)
    replacements = 0

    def sub_in_segment(segment: str, in_trigger: bool) -> str:
        nonlocal replacements

        def repl_prop(m: re.Match) -> str:
            nonlocal replacements
            prop, quote, val = m.group(1), m.group(2), m.group(3)
            norm = normalize_hex(val)
            if norm not in hex_to_res:
                return m.group(0)
            key = hex_to_res[norm]
            static = use_static or in_trigger
            if prop == "Color":
                new_val = resource_markup_color(key, static)
            else:
                new_val = resource_markup(key, static)
            replacements += 1
            return f'{prop}={quote}{new_val}{quote}'

        segment = re.sub(
            r'(Color|Background|Foreground|BorderBrush|Fill|Stroke|CaretBrush)=(["\'])(#[0-9A-Fa-f]{3,8})\2',
            repl_prop,
            segment,
        )

        def repl_value(m: re.Match) -> str:
            nonlocal replacements
            val = m.group(1)
            norm = normalize_hex(val)
            if norm not in hex_to_res:
                return m.group(0)
            key = hex_to_res[norm]
            static = use_static or in_trigger
            replacements += 1
            return f'Value="{resource_markup(key, static)}"'

        segment = re.sub(
            r'Value="(#[0-9A-Fa-f]{3,8})"',
            repl_value,
            segment,
        )
        return segment

    # Split by triggers to force StaticResource inside triggers even in views
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

    out = "".join(sub_in_segment(seg, trig) for seg, trig in parts)
    return out, replacements


def replace_hex_in_cs(content: str, hex_to_res: dict[str, str]) -> tuple[str, int]:
    replacements = 0

    def repl(m: re.Match) -> str:
        nonlocal replacements
        val = m.group(0)
        norm = normalize_hex(val)
        if norm not in hex_to_res:
            return val
        key = hex_to_res[norm]
        replacements += 1
        return f'ColorSchemeResourceCatalog.GetBrush("{key}")'

    # Skip comments/strings that are not colors - only replace in Color.From*, BrushConverter, obvious hex strings
    patterns = [
        (r'Color\.FromRgb\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*\)', None),
        (r'new BrushConverter\(\)\.ConvertFrom\("(#[0-9A-Fa-f]{3,8})"\)', 1),
        (r'"(#[0-9A-Fa-f]{3,8})"', 1),
    ]
    out = content
    for pat, _ in patterns:
        pass  # handled below

    def repl_quoted(m: re.Match) -> str:
        nonlocal replacements
        val = m.group(1)
        norm = normalize_hex(val)
        if norm not in hex_to_res:
            return m.group(0)
        key = hex_to_res[norm]
        replacements += 1
        return f'ColorSchemeResourceCatalog.GetBrush("{key}")'

    out = re.sub(r'"(#[0-9A-Fa-f]{3,8})"', repl_quoted, out)
    out = re.sub(
        r'new BrushConverter\(\)\.ConvertFrom\("(#[0-9A-Fa-f]{3,8})"\)!',
        lambda m: f'ColorSchemeResourceCatalog.GetBrush("{hex_to_res[normalize_hex(m.group(1))]}")',
        out,
    )
    return out, replacements


def append_tokens(light_text: str, new_tokens: dict[str, str]) -> str:
    if not new_tokens:
        return light_text
    lines = ["", "    <!-- #region Auto-migrated tokens -->"]
    for hex_norm in sorted(new_tokens.keys(), key=lambda h: new_tokens[h]):
        key = new_tokens[hex_norm]
        lines.append(f'    <SolidColorBrush x:Key="{key}">{hex_norm}</SolidColorBrush>')
        if not key.endswith("_Cl"):
            lines.append(f'    <Color x:Key="{key}_Cl">{hex_norm}</Color>')
    lines.append("    <!-- #endregion -->")
    insert = "\n".join(lines) + "\n"
    return light_text.replace("</ResourceDictionary>", insert + "</ResourceDictionary>")


def sync_dark_from_light() -> None:
    light = LIGHT.read_text(encoding="utf-8")
    dark = light.replace('<sys:String x:Key="ColorSchemeName">Light</sys:String>',
                         '<sys:String x:Key="ColorSchemeName">Dark</sys:String>')
    dark = dark.replace("ListViewItemColors.Light.xaml", "ListViewItemColors.Dark.xaml")
    DARK.write_text(dark, encoding="utf-8")

    lv_light = LV_LIGHT.read_text(encoding="utf-8")
    lv_dark = lv_light.replace('<sys:String x:Key="ColorSchemeName">Light</sys:String>',
                               '<sys:String x:Key="ColorSchemeName">Dark</sys:String>')
    LV_DARK.write_text(lv_dark, encoding="utf-8")


def main() -> None:
    light_text = read_text(LIGHT)
    existing = parse_light_keys(light_text)

    # Seed aliases into mapping
    hex_to_key_map: dict[str, str] = {}
    for hex_val, key in ALIASES.items():
        hex_to_key_map[normalize_hex(hex_val)] = key
    for hex_val, key in existing.items():
        hex_to_key_map.setdefault(hex_val, key)

    # Collect all hex from source files
    found: set[str] = set()
    for path in iter_source_files():
        text = read_text(path)
        for m in HEX_RE.finditer(text):
            found.add(normalize_hex(m.group(0)))

    new_tokens: dict[str, str] = {}
    for hex_norm in sorted(found):
        if hex_norm not in hex_to_key_map:
            key = hex_to_key(hex_norm)
            # avoid collision
            base = key
            i = 2
            existing_keys = set(hex_to_key_map.values()) | set(new_tokens.values())
            while key in existing_keys:
                key = f"{base}_{i}"
                i += 1
            new_tokens[hex_norm] = key
            hex_to_key_map[hex_norm] = key

    light_text = append_tokens(light_text, new_tokens)
    write_text(LIGHT, light_text)

    total_repl = 0
    for path in iter_source_files():
        original = read_text(path)
        if path.suffix.lower() == ".xaml":
            updated, n = replace_hex_in_xaml(original, path, hex_to_key_map)
        elif should_process_cs(path):
            updated, n = replace_hex_in_cs(original, hex_to_key_map)
        else:
            continue
        if updated != original:
            write_text(path, updated)
            total_repl += n
            print(f"Updated {path.relative_to(ROOT)} ({n} refs)")

    sync_dark_from_light()
    print(f"\nAdded {len(new_tokens)} new tokens to Light.xaml")
    print(f"Total replacements: {total_repl}")
    print("Synced Dark.xaml and ListViewItemColors.Dark.xaml from Light")


if __name__ == "__main__":
    main()
