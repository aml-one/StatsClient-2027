#!/usr/bin/env python3
"""Full color-scheme validation: hex, merge order, missing keys, _Cl companions."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient"
SCHEME = ROOT / "Themes" / "ColorSchemes"

KEY_RE = re.compile(r'x:Key="([^"]+)"')
RES_RE = re.compile(r"\{(StaticResource|DynamicResource)\s+([^}]+)\}")
HEX_RE = re.compile(r'(?:>|="|="#)(#[0-9A-Fa-f]+)(?:<|")')
MERGE_RE = re.compile(r'Source="([^"]+)"')
TYPE_RE = [
    (re.compile(r'<SolidColorBrush x:Key="([^"]+)"'), "brush"),
    (re.compile(r'<Color x:Key="([^"]+)"'), "color"),
    (re.compile(r'<sys:String x:Key="([^"]+)"'), "string"),
]

SYSTEM_PREFIXES = ("x:", "{", "TemplateBinding", "Binding", "RelativeSource")


def parse_dict(path: Path) -> tuple[dict[str, str], list[Path]]:
    text = path.read_text(encoding="utf-8", errors="replace")
    types: dict[str, str] = {}
    for pattern, kind in TYPE_RE:
        for key in pattern.findall(text):
            types[key] = kind
    merges: list[Path] = []
    for rel in MERGE_RE.findall(text):
        mp = path.parent / Path(rel.replace("/Themes/ColorSchemes/", "").lstrip("/"))
        if not mp.exists():
            mp = ROOT / rel.lstrip("/")
        if mp.exists():
            merges.append(mp)
    return types, merges


def load_scheme_keys(path: Path, stack: set[Path] | None = None) -> set[str]:
    if stack is None:
        stack = set()
    if path in stack:
        return set()
    stack.add(path)
    keys: set[str] = set(KEY_RE.findall(path.read_text(encoding="utf-8", errors="replace")))
    _, merges = parse_dict(path)
    for merge in merges:
        keys.update(load_scheme_keys(merge, stack))
    return keys


def keys_visible_at_merge(parent: Path, merge_index: int) -> set[str]:
    """Keys available when WPF loads parent.MergedDictionaries[merge_index]."""
    text = parent.read_text(encoding="utf-8", errors="replace")
    merges = MERGE_RE.findall(text)
    visible: set[str] = set()
    for rel in merges[:merge_index]:
        mp = parent.parent / Path(rel.replace("/Themes/ColorSchemes/", "").lstrip("/"))
        if not mp.exists():
            mp = ROOT / rel.lstrip("/")
        if mp.exists():
            visible.update(load_scheme_keys(mp))
    return visible


def static_refs_in_file(path: Path) -> list[tuple[int, str, str]]:
    out: list[tuple[int, str, str]] = []
    for i, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        for kind, key in RES_RE.findall(line):
            if kind == "StaticResource":
                key = key.strip()
                if not key.startswith(SYSTEM_PREFIXES):
                    out.append((i, key, line.strip()))
    return out


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []

    light = SCHEME / "Light.xaml"
    all_keys = load_scheme_keys(light)
    all_types: dict[str, str] = {}
    for path in SCHEME.glob("*.xaml"):
        types, _ = parse_dict(path)
        all_types.update(types)
        all_keys.update(KEY_RE.findall(path.read_text(encoding="utf-8", errors="replace")))

    # 1) Invalid hex in scheme files
    for path in SCHEME.glob("*.xaml"):
        for i, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            for hx in HEX_RE.findall(line):
                n = len(hx) - 1  # minus #
                if n not in (3, 4, 6, 8):
                    errors.append(f"{path.name}:{i} invalid hex '{hx}' ({n} digits)")

    # 2) StaticResource merge-order in merged scheme dictionaries
    merges = MERGE_RE.findall(light.read_text(encoding="utf-8"))
    for idx, rel in enumerate(merges):
        mp = light.parent / Path(rel.replace("/Themes/ColorSchemes/", "").lstrip("/"))
        if not mp.exists():
            mp = ROOT / rel.lstrip("/")
        if not mp.exists():
            errors.append(f"Light.xaml merge target missing: {rel}")
            continue
        visible = keys_visible_at_merge(light, idx)
        defined_in_file: set[str] = set()
        for line_no, line in enumerate(mp.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
            for pattern, _ in TYPE_RE:
                defined_in_file.update(pattern.findall(line))
            for kind, key in RES_RE.findall(line):
                if kind != "StaticResource":
                    continue
                key = key.strip()
                if key.startswith(SYSTEM_PREFIXES):
                    continue
                if key not in visible and key not in defined_in_file:
                    errors.append(
                        f"MERGE ORDER {mp.name}:{line_no} StaticResource '{key}' not visible at load time"
                    )

    # 3) StaticResource in Light.xaml body referencing keys only in later parent entries (same-file order)
    parent_types, _ = parse_dict(light)
    parent_lines = light.read_text(encoding="utf-8").splitlines()
    defined_so_far: set[str] = set(keys_visible_at_merge(light, len(merges)))
    for i, line in enumerate(parent_lines, 1):
        if "MergedDictionaries" in line:
            continue
        for pattern, _ in TYPE_RE:
            for key in pattern.findall(line):
                defined_so_far.add(key)
        for kind, key in RES_RE.findall(line):
            if kind != "StaticResource":
                continue
            key = key.strip()
            if key.startswith(SYSTEM_PREFIXES):
                continue
            if key not in defined_so_far and key not in all_keys:
                errors.append(f"Light.xaml:{i} StaticResource '{key}' not defined yet")

    # 4) DynamicResource *_Cl / scheme refs in views must exist
    scheme_prefixes = (
        "Window", "Primary", "Black", "White", "Red", "Green", "Blue", "Gray", "Named",
        "Immersive", "Splash", "Busy", "Dialog", "Modal", "Glass", "Viewer", "Lv",
        "Settings", "Tab", "Vita", "Filter", "Semantic", "Code", "Health", "Payment",
        "Age", "Order", "Validation", "Inconsistency", "Glow", "Outline", "Event",
        "Pan", "Comment", "Blink", "SchemeColor", "Accent", "Esl", "CircleCheckBox",
        "Expander", "SearchBox", "ComboBox", "Classic", "Transparent", "Light",
        "Dark", "Empty", "Cream", "Home", "Archive", "OrderInfo", "Loading",
        "Control", "Border", "Disabled", "MessageBox", "Popup", "ShowRx", "ThreeShape",
        "AccountInfo", "HealthReport", "Inconsistency", "PaymentStatus", "GlowColor",
        "Semantic", "TabLegacy", "TabShape", "TodayButton", "Vita",
    )
    include = ("Themes/", "MVVM/View/", "DcmViewer/", "UserControls/")
    for path in ROOT.rglob("*.xaml"):
        if any(p in path.parts for p in ("obj", "bin", "ColorSchemes")):
            continue
        rel = path.relative_to(ROOT).as_posix()
        if not rel.startswith(include):
            continue
        for i, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            for kind, key in RES_RE.findall(line):
                key = key.strip()
                if not any(key.startswith(p) for p in scheme_prefixes):
                    continue
                if key.endswith("_Cl") and key not in all_keys:
                    # allow if base Color exists
                    base = key[:-3]
                    if base not in all_keys:
                        errors.append(f"{rel}:{i} missing {kind} '{key}'")
                elif kind == "DynamicResource" and key not in all_keys:
                    errors.append(f"{rel}:{i} missing DynamicResource '{key}'")

    # 5) Missing _Cl for Color keys used as DynamicResource *_Cl
    color_keys = {k for k, t in all_types.items() if t == "color" and not k.endswith("_Cl")}
    for ck in sorted(color_keys):
        cl = f"{ck}_Cl"
        if cl not in all_keys:
            # only warn if referenced
            pass

    missing_cl: list[str] = []
    for path in ROOT.rglob("*.xaml"):
        if any(p in path.parts for p in ("obj", "bin")):
            continue
        text = path.read_text(encoding="utf-8")
        for key in re.findall(r"\{DynamicResource\s+(\w+_Cl)\}", text):
            if key not in all_keys:
                base = key[:-3]
                if base in all_keys:
                    missing_cl.append(f"{key} (base {base} exists)")

    for key in sorted(set(missing_cl)):
        errors.append(f"MISSING _Cl companion: {key}")

    # 6) Color vs Brush type mismatches on dependency properties
    brush_prop_re = re.compile(
        r'(Background|BorderBrush|Foreground|Fill|Stroke|SelectionBrush|CaretBrush)="\{(StaticResource|DynamicResource)\s+([^}]+)\}"'
    )
    setter_brush_re = re.compile(
        r'Property="(Background|BorderBrush|Foreground|Fill|Stroke|SelectionBrush|CaretBrush)"\s+Value="\{(StaticResource|DynamicResource)\s+([^}]+)\}"'
    )
    for path in ROOT.rglob("*.xaml"):
        if any(p in path.parts for p in ("obj", "bin")):
            continue
        rel = path.relative_to(ROOT).as_posix()
        for i, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
            if "GradientStop" in line or "DropShadowEffect" in line or 'SolidColorBrush Color=' in line:
                for kind, key in RES_RE.findall(line):
                    if all_types.get(key) == "brush" and ("Color=" in line or ".Color" in line):
                        errors.append(f"{rel}:{i} brush '{key}' used on Color property")
            for prop, kind, key in brush_prop_re.findall(line):
                key = key.strip()
                if key.endswith("_Cl") or all_types.get(key) == "color":
                    base = key[:-3] if key.endswith("_Cl") else key
                    hint = f" use '{base}' brush key" if base in all_types else ""
                    errors.append(
                        f"{rel}:{i} Color resource '{key}' on {prop} — use SolidColorBrush key instead{hint}"
                    )
            for prop, kind, key in setter_brush_re.findall(line):
                key = key.strip()
                if key.endswith("_Cl") or all_types.get(key) == "color":
                    base = key[:-3] if key.endswith("_Cl") else key
                    hint = f" use '{base}' brush key" if base in all_types else ""
                    errors.append(
                        f"{rel}:{i} Color resource '{key}' on Setter {prop} — use SolidColorBrush key instead{hint}"
                    )
            if "DefaultBackgroundColor=" in line:
                for kind, key in RES_RE.findall(line):
                    errors.append(
                        f"{rel}:{i} WebView2 DefaultBackgroundColor cannot use {{{kind} {key}}} — set System.Drawing.Color in code-behind"
                    )

    # 6b) Every *_Cl color key used on brush properties must have a non-_Cl SolidColorBrush companion
    brush_only_usages: dict[str, list[str]] = {}
    for path in ROOT.rglob("*.xaml"):
        if any(p in path.parts for p in ("obj", "bin", "ColorSchemes")):
            continue
        rel = path.relative_to(ROOT).as_posix()
        text = path.read_text(encoding="utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            for prop, _, key in brush_prop_re.findall(line):
                if key.endswith("_Cl"):
                    brush_only_usages.setdefault(key, []).append(f"{rel}:{i}:{prop}")
            for prop, _, key in setter_brush_re.findall(line):
                if key.endswith("_Cl"):
                    brush_only_usages.setdefault(key, []).append(f"{rel}:{i}:Setter.{prop}")
    for cl_key, usages in sorted(brush_only_usages.items()):
        base = cl_key[:-3]
        if base not in all_types:
            errors.append(
                f"MISSING brush companion '{base}' for Color '{cl_key}' (used on brush props: {usages[0]})"
            )

    # 7) ColorSchemeResourceCatalog usage in C#
    cs_catalog_key = re.compile(
        r'ColorSchemeResourceCatalog\.(?:GetBrush(?:OrDefault)?|GetColor|GetHex|GetNamedColorString(?:OrHex)?|TryGetBrush|TryGetColor)\("([^"]+)"'
    )
    cs_get_brush = re.compile(r'ColorSchemeResourceCatalog\.GetBrush(?:OrDefault)?\("([^"]+)"\)')
    cs_get_color = re.compile(
        r'ColorSchemeResourceCatalog\.(?:GetColor|GetHex)\("([^"]+)"\)'
    )
    cs_brush_prop_assign = re.compile(
        r'\.(Background|BorderBrush|Foreground|Fill|Stroke|SelectionBrush|CaretBrush)\s*=\s*'
        r'ColorSchemeResourceCatalog\.GetBrush(?:OrDefault)?\("([^"]+)"\)'
    )
    for path in ROOT.rglob("*.cs"):
        if any(p in path.parts for p in ("obj", "bin")):
            continue
        rel = path.relative_to(ROOT).as_posix()
        text = path.read_text(encoding="utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            for key in cs_catalog_key.findall(line):
                if key not in all_keys:
                    errors.append(f"{rel}:{i} C# catalog key '{key}' missing from scheme")

            for key in cs_get_brush.findall(line):
                if key.endswith("_Cl"):
                    base = key[:-3]
                    hint = f" use GetBrush('{base}')" if base in all_types else f" use GetColor('{key}')"
                    errors.append(
                        f"{rel}:{i} GetBrush('{key}') — *_Cl keys are Color; use brush key without _Cl{hint}"
                    )
                elif all_types.get(key) == "color":
                    brush_alt = f"{key.replace('Color', 'Brush')}" if key.endswith("Color") else f"{key}Brush"
                    hints: list[str] = []
                    if brush_alt in all_types:
                        hints.append(f"GetBrush('{brush_alt}')")
                    if f"{key.replace('Color', '')}Brush" in all_types:
                        pass
                    if "WindowBackgroundBrush" in all_types and key == "WindowBackgroundColor":
                        hints.append("GetBrush('WindowBackgroundBrush')")
                    if "ClassicWindowBackgroundBrush" in all_types and key == "ClassicWindowBackgroundColor":
                        hints.append("GetBrush('ClassicWindowBackgroundBrush')")
                    hint = f" ({', '.join(hints)})" if hints else ""
                    errors.append(
                        f"{rel}:{i} GetBrush('{key}') — scheme type is Color; use GetColor or a *Brush key{hint}"
                    )

            # GetColor/GetHex on SolidColorBrush keys is OK — GetColor reads brush.Color at runtime.

            for _prop, key in cs_brush_prop_assign.findall(line):
                if key.endswith("_Cl") or all_types.get(key) == "color":
                    base = key[:-3] if key.endswith("_Cl") else key
                    hint = f" use '{base}'" if base in all_types and all_types.get(base) == "brush" else ""
                    errors.append(
                        f"{rel}:{i} brush property assigned GetBrush('{key}') — Color resource{hint}"
                    )

    # 8) Light/Dark scheme parity (same keys and resource types)
    dark = SCHEME / "Dark.xaml"
    lv_dark = SCHEME / "ListViewItemColors.Dark.xaml"
    scheme_pairs = [
        (light, dark, "Dark.xaml"),
        (SCHEME / "ListViewItemColors.Light.xaml", lv_dark, "ListViewItemColors.Dark.xaml"),
    ]
    for light_path, dark_path, dark_label in scheme_pairs:
        light_keys = set(KEY_RE.findall(light_path.read_text(encoding="utf-8", errors="replace")))
        dark_keys = set(KEY_RE.findall(dark_path.read_text(encoding="utf-8", errors="replace")))
        for key in sorted(light_keys - dark_keys):
            errors.append(f"Light/Dark key missing in {dark_label}: '{key}'")
        for key in sorted(dark_keys - light_keys):
            errors.append(f"Light/Dark extra key in {dark_label}: '{key}'")
        light_types, _ = parse_dict(light_path)
        dark_types, _ = parse_dict(dark_path)
        for key in sorted(light_keys & dark_keys):
            lt, dt = light_types.get(key), dark_types.get(key)
            if lt and dt and lt != dt:
                errors.append(f"Light/Dark type mismatch for '{key}': Light={lt}, Dark={dt}")

    dark_text = dark.read_text(encoding="utf-8", errors="replace")
    if 'ColorSchemeName">Dark<' not in dark_text:
        errors.append("Dark.xaml ColorSchemeName must be 'Dark'")
    if "ListViewItemColors.Dark.xaml" not in dark_text:
        errors.append("Dark.xaml must merge ListViewItemColors.Dark.xaml")
    if "ListViewItemColors.Light.xaml" in dark_text:
        errors.append("Dark.xaml must not reference ListViewItemColors.Light.xaml")

    print(f"Scheme keys loaded: {len(all_keys)}")
    print(f"Errors: {len(errors)}")
    for e in sorted(set(errors))[:80]:
        print(f"  ERROR: {e}")
    if len(errors) > 80:
        print(f"  ... and {len(errors) - 80} more")

    if errors:
        return 1
    print("All scheme validations passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
