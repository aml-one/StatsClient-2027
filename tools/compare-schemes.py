#!/usr/bin/env python3
"""Compare Light vs Dark color scheme keys and structure."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient" / "Themes" / "ColorSchemes"
KEY_RE = re.compile(r'x:Key="([^"]+)"')
TYPE_PATTERNS = [
    (re.compile(r"<SolidColorBrush x:Key=\"([^\"]+)\""), "brush"),
    (re.compile(r"<Color x:Key=\"([^\"]+)\""), "color"),
    (re.compile(r"<sys:String x:Key=\"([^\"]+)\""), "string"),
]


def parse_types(path: Path) -> dict[str, str]:
    text = path.read_text(encoding="utf-8", errors="replace")
    types: dict[str, str] = {}
    for pattern, kind in TYPE_PATTERNS:
        for key in pattern.findall(text):
            types[key] = kind
    return types


def keys(path: Path) -> set[str]:
    return set(KEY_RE.findall(path.read_text(encoding="utf-8", errors="replace")))


def main() -> int:
    errors: list[str] = []
    pairs = [
        ("Light.xaml", "Dark.xaml"),
        ("ListViewItemColors.Light.xaml", "ListViewItemColors.Dark.xaml"),
    ]

    for light_name, dark_name in pairs:
        light_path = ROOT / light_name
        dark_path = ROOT / dark_name
        light_keys = keys(light_path)
        dark_keys = keys(dark_path)
        only_light = sorted(light_keys - dark_keys)
        only_dark = sorted(dark_keys - light_keys)

        print(f"=== {light_name} vs {dark_name} ===")
        print(f"  Light: {len(light_keys)} keys | Dark: {len(dark_keys)} keys")

        if only_light:
            errors.append(f"{dark_name} missing keys: {only_light}")
            print(f"  MISSING in Dark ({len(only_light)}): {only_light[:15]}")
            if len(only_light) > 15:
                print(f"    ... and {len(only_light) - 15} more")
        if only_dark:
            errors.append(f"{dark_name} extra keys: {only_dark}")
            print(f"  EXTRA in Dark ({len(only_dark)}): {only_dark[:15]}")

        light_types = parse_types(light_path)
        dark_types = parse_types(dark_path)
        for key in sorted(light_keys & dark_keys):
            lt = light_types.get(key)
            dt = dark_types.get(key)
            if lt and dt and lt != dt:
                errors.append(f"Type mismatch for '{key}': Light={lt}, Dark={dt}")
                print(f"  TYPE MISMATCH: {key} Light={lt} Dark={dt}")

        if not only_light and not only_dark:
            print("  All keys match")

        if dark_name == "Dark.xaml":
            dark_text = dark_path.read_text(encoding="utf-8")
            if 'ColorSchemeName">Dark<' not in dark_text:
                errors.append("Dark.xaml ColorSchemeName is not 'Dark'")
            if "ListViewItemColors.Dark.xaml" not in dark_text:
                errors.append("Dark.xaml does not merge ListViewItemColors.Dark.xaml")
            if "ListViewItemColors.Light.xaml" in dark_text:
                errors.append("Dark.xaml still references ListViewItemColors.Light.xaml")

        print()

    if errors:
        print(f"FAILED: {len(errors)} issue(s)")
        return 1

    print("Light/Dark scheme keys and types are aligned.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
