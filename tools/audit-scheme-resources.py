#!/usr/bin/env python3
"""Verify color-scheme StaticResource/DynamicResource keys exist."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient"
SCHEME = ROOT / "Themes" / "ColorSchemes"
KEY_RE = re.compile(r'x:Key="([^"]+)"')
RES_RE = re.compile(r"\{(StaticResource|DynamicResource)\s+([^}]+)\}")

# Only audit theme + view files that use scheme tokens (not styles/converters)
INCLUDE_PREFIXES = (
    "Themes/",
    "MVVM/View/",
    "DcmViewer/",
    "UserControls/",
)

SCHEME_PREFIXES = (
    "Window", "Primary", "Black", "White", "Red", "Green", "Blue", "Gray", "Named",
    "Immersive", "Splash", "Busy", "Dialog", "Modal", "Glass", "Viewer", "Lv",
    "Settings", "Tab", "Vita", "Filter", "Semantic", "Code", "Health", "Payment",
    "Age", "Order", "Validation", "Inconsistency", "Glow", "Outline", "Event",
    "Pan", "Comment", "Blink", "SchemeColor", "Accent", "Esl", "CircleCheckBox",
    "Expander", "SearchBox", "ComboBox", "Classic", "Transparent", "Light",
    "Dark", "Empty", "Cream", "Home", "Archive", "OrderInfo", "Loading",
)


def is_scheme_key(key: str) -> bool:
    return any(key.startswith(p) for p in SCHEME_PREFIXES)


def collect_scheme_keys() -> set[str]:
    keys: set[str] = set()
    for path in SCHEME.glob("*.xaml"):
        keys.update(KEY_RE.findall(path.read_text(encoding="utf-8", errors="replace")))
    return keys


def main() -> None:
    keys = collect_scheme_keys()
    missing: dict[str, list[str]] = {}
    for path in ROOT.rglob("*.xaml"):
        if any(p in path.parts for p in ("obj", "bin", "ColorSchemes")):
            continue
        rel = path.relative_to(ROOT).as_posix()
        if not rel.startswith(INCLUDE_PREFIXES):
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        for kind, key in RES_RE.findall(text):
            key = key.strip()
            if not is_scheme_key(key):
                continue
            if key not in keys:
                missing.setdefault(key, []).append(f"{rel} ({kind})")

    print(f"Scheme color keys: {len(keys)}")
    print(f"Missing scheme color refs: {len(missing)}")
    for key in sorted(missing):
        print(f"  {key}: {missing[key][0]} (+{len(missing[key]) - 1})")
    if missing:
        raise SystemExit(1)
    print("All scheme color StaticResource/DynamicResource keys resolve.")


if __name__ == "__main__":
    main()
