#!/usr/bin/env python3
"""Add missing *_Cl Color companions for SolidColorBrush keys in Light.xaml."""

import re
from pathlib import Path

LIGHT = Path(__file__).resolve().parent.parent / "StatsClient" / "Themes" / "ColorSchemes" / "Light.xaml"
text = LIGHT.read_text(encoding="utf-8")
existing = set(re.findall(r'x:Key="([^"]+)"', text))

brush_re = re.compile(
    r'<SolidColorBrush x:Key="([^"]+)"(?:\s+Color="([^"]+)"|>([^<]+)<)',
)
to_add: list[tuple[str, str]] = []
for m in brush_re.finditer(text):
    key, color_attr, inner = m.group(1), m.group(2), m.group(3)
    if key.endswith("_Cl"):
        continue
    cl_key = f"{key}_Cl"
    if cl_key in existing:
        continue
    val = (color_attr or inner or "").strip()
    if not val:
        continue
    if val.startswith("#") or val[0].isalpha():
        to_add.append((cl_key, val))

color_re = re.compile(r'<Color x:Key="([^"]+)"(?:>([^<]+)<|\s+Color="([^"]+)")')
for m in color_re.finditer(text):
    key, inner, attr = m.group(1), m.group(2), m.group(3)
    if not key.endswith("_Cl"):
        continue
    val = (inner or attr or "").strip()
    base = key[:-3]
    cl_key = f"{base}_Cl"
    if base + "_Cl" == key and f"{base}" not in [k for k, _ in to_add]:
        pass  # already color

# Also add explicit Color-only keys from Light.xaml Color entries without brush _Cl
for m in color_re.finditer(text):
    key = m.group(1)
    if not key.endswith("_Cl"):
        base_cl = f"{key}_Cl" if not key.endswith("Color") else None
        if base_cl and base_cl not in existing:
            val = (m.group(2) or m.group(3) or "").strip()
            if val:
                to_add.append((base_cl, val))

# Dedupe
seen = set()
unique = []
for k, v in to_add:
    if k in existing or k in seen:
        continue
    seen.add(k)
    unique.append((k, v))

if not unique:
    print("No missing _Cl companions.")
else:
    block = "\n    <!-- #region Auto-added _Cl companions -->\n"
    for k, v in sorted(unique):
        block += f'    <Color x:Key="{k}">{v}</Color>\n'
    block += "    <!-- #endregion -->\n"
    text = text.replace("</ResourceDictionary>", block + "</ResourceDictionary>")
    LIGHT.write_text(text, encoding="utf-8")
    print(f"Added {len(unique)} _Cl companions to Light.xaml")
