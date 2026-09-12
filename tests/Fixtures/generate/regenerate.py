"""Regenerates every fixture kind: python tests/Fixtures/generate/regenerate.py [--check] [kind ...]

Without --check, fixtures whose content changed are rewritten (with today's date) and stale
files are removed; unchanged fixtures are left byte for byte. With --check nothing is written
and the exit code is 1 when any fixture would change, is missing or is stale.
Run it with the interpreter of the generator's environment (requirements.txt).
"""
from __future__ import annotations

import sys

import constants
import propellants
import rp1311
import thermo_functions
import transport_fits
from writer import Writer

SCRIPTS = [constants, thermo_functions, transport_fits, rp1311, propellants]


def main() -> int:
    args = sys.argv[1:]
    check = "--check" in args
    only = [a for a in args if not a.startswith("--")]
    writer = Writer(check=check, only=only or None)
    for script in SCRIPTS:
        script.generate(writer)
    return writer.finish()


if __name__ == "__main__":
    sys.exit(main())
