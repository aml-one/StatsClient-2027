#!/usr/bin/env python3
"""Static smoke scan for common WPF color-scheme runtime crashes."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient"
SCHEME = ROOT / "Themes" / "ColorSchemes"

KEY_RE = re.compile(r'x:Key="([^"]+)"')
RES_RE = re.compile(r"\{(StaticResource|DynamicResource)\s+([^}]+)\}")
HEX_RE = re.compile(r'(?:>|="|="#)(#[0-9A-Fa-f]+)(?:<|")')
BRUSH_PROP_RE = re.compile(
    r'(Background|BorderBrush|Foreground|Fill|Stroke)="\{(StaticResource|DynamicResource)\s+([^}]+)\}"'
)
COLOR_PROP_RE = re.compile(
    r'(Color|DefaultBackgroundColor|BackgroundColor)="\{(StaticResource|DynamicResource)\s+([^}]+)\}"'
)


def load_types() -> dict[str, str]:
    types: dict[str, str] = {}
    for path in SCHEME.glob("*.xaml"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for key in re.findall(r'<SolidColorBrush x:Key="([^"]+)"', text):
            types[key] = "brush"
        for key in re.findall(r'<Color x:Key="([^"]+)"', text):
            types[key] = "color"
    return types


def main() -> int:
    types = load_types()
    all_keys = set(types)
    for path in SCHEME.glob("*.xaml"):
        all_keys.update(KEY_RE.findall(path.read_text(encoding="utf-8", errors="replace")))

    errors: list[str] = []

    for path in ROOT.rglob("*.xaml"):
        if any(p in path.parts for p in ("obj", "bin")):
            continue
        rel = path.relative_to(ROOT).as_posix()
        for i, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
            for hx in HEX_RE.findall(line):
                if len(hx) - 1 not in (3, 4, 6, 8):
                    errors.append(f"{rel}:{i} invalid hex '{hx}'")
            for prop, kind, key in BRUSH_PROP_RE.findall(line):
                key = key.strip()
                if key.endswith("_Cl") or types.get(key) == "color":
                    errors.append(f"{rel}:{i} Color '{key}' on {prop} — use *Brush")
            for prop, kind, key in COLOR_PROP_RE.findall(line):
                key = key.strip()
                if types.get(key) == "brush":
                    errors.append(f"{rel}:{i} Brush '{key}' on {prop} — use *Color or *_Cl")
                if prop == "DefaultBackgroundColor":
                    errors.append(
                        f"{rel}:{i} WebView2 DefaultBackgroundColor must be set in code-behind (not {{{kind} {key}}})"
                    )
            for kind, key in RES_RE.findall(line):
                key = key.strip()
                if key.startswith("{") or key in ("x:Static",):
                    continue
                if key not in all_keys and not key.endswith("Converter") and "Binding" not in key:
                    # local resources in same file may exist — only flag scheme-like keys
                    prefixes = (
                        "Window", "Splash", "Busy", "Immersive", "Lv", "SchemeColor",
                        "Classic", "Primary", "Accent", "Viewer", "Semantic", "Payment",
                        "White", "Black", "Transparent", "Popup", "Dialog", "Settings",
                    )
                    if any(key.startswith(p) for p in prefixes):
                        errors.append(f"{rel}:{i} missing resource '{key}' ({kind})")

    print(f"Scanned XAML under {ROOT.name}")
    print(f"Errors: {len(errors)}")
    for e in sorted(set(errors))[:100]:
        print(f"  {e}")
    if len(errors) > 100:
        print(f"  ... and {len(errors) - 100} more")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
