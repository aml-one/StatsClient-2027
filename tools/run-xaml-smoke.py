#!/usr/bin/env python3
"""Run in-process XAML smoke test via StatsClient.exe (STATS_XAML_SMOKE=1)."""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EXE = ROOT / "StatsClient" / "bin" / "Debug" / "net10.0-windows7.0" / "StatsClient.exe"
PROJECT = ROOT / "StatsClient" / "StatsClient.csproj"


def main() -> int:
    if not EXE.exists():
        print("Building StatsClient (Debug)...")
        build = subprocess.run(
            ["dotnet", "build", str(PROJECT), "-c", "Debug"],
            cwd=ROOT,
            check=False,
        )
        if build.returncode != 0:
            return build.returncode

    env = os.environ.copy()
    env["STATS_XAML_SMOKE"] = "1"
    print(f"Running XAML smoke test: {EXE}")
    result = subprocess.run([str(EXE)], cwd=EXE.parent, env=env, check=False)
    log = EXE.parent / "xaml-smoke.log"
    if log.exists():
        print(log.read_text(encoding="utf-8", errors="replace"))
    return result.returncode


if __name__ == "__main__":
    sys.exit(main())
