#!/usr/bin/env python3
"""Verify StaticResource/DynamicResource keys exist in color scheme files."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "StatsClient"
SCHEME = ROOT / "Themes" / "ColorSchemes"
KEY_RE = re.compile(r'x:Key="([^"]+)"')
RES_RE = re.compile(r"\{(StaticResource|DynamicResource)\s+([^}]+)\}")

SYSTEM_KEYS = {
    "x:Static", "RelativeSource", "TemplateBinding", "Binding",
}


def collect_scheme_keys() -> set[str]:
    keys: set[str] = set()
    for path in SCHEME.glob("*.xaml"):
        keys.update(KEY_RE.findall(path.read_text(encoding="utf-8", errors="replace")))
    return keys


def collect_references() -> dict[str, list[str]]:
    refs: dict[str, list[str]] = {}
    for path in ROOT.rglob("*.xaml"):
        if any(p in path.parts for p in ("obj", "bin")):
            continue
        rel = path.relative_to(ROOT).as_posix()
        text = path.read_text(encoding="utf-8", errors="replace")
        for kind, key in RES_RE.findall(text):
            key = key.strip()
            if key.startswith("{") or key in SYSTEM_KEYS:
                continue
            refs.setdefault(key, []).append(f"{rel} ({kind})")
    return refs


def main() -> None:
    keys = collect_scheme_keys()
    refs = collect_references()
    missing = []
    for key, locations in sorted(refs.items()):
        if key not in keys:
            # keys defined in local ResourceDictionary are ok; flag only app-wide misses
            missing.append((key, locations[:3], len(locations)))

    print(f"Scheme keys: {len(keys)}")
    print(f"Referenced keys: {len(refs)}")
    print(f"Missing from scheme files: {len(missing)}")
    for key, locs, total in missing[:40]:
        print(f"  MISSING: {key} ({total} uses) e.g. {locs[0]}")
    if len(missing) > 40:
        print(f"  ... and {len(missing) - 40} more")
    if missing:
        raise SystemExit(1)
    print("All StaticResource/DynamicResource keys resolve in ColorSchemes.")


if __name__ == "__main__":
    main()
