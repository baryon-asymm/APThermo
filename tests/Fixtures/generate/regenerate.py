"""Regenerates every fixture kind: python tests/Fixtures/generate/regenerate.py [--check] [--sample] [kind ...]

Without --check, fixtures whose content changed are rewritten (with today's date) and stale
files are removed; unchanged fixtures are left byte for byte. With --check nothing is written
and the exit code is 1 when any fixture would change, is missing or is stale.

--sample restricts every kind to a sample chosen from the committed directory listing
(tests/Fixtures/BOOT.md, "The outputs are bound to the generator"): at least one case of every
kind, and every case of the `throat` kind. Meant for --check on CI, where regenerating and
comparing every fixture is too slow for every push; it binds the outputs to the generator
without paying that cost. Combine with a `kind` filter to sample only those kinds.

Run it with the interpreter of the generator's environment (requirements.txt).
"""
from __future__ import annotations

import os
import sys

import condensed_phase_limit
import constants
import low_temperature
import plateaus
import propellants
import retention_threshold
import rp1311
import thermo_functions
import throat_scan
import transport_fits
from common import CASES
from writer import Writer

SCRIPTS = [constants, thermo_functions, transport_fits, rp1311, propellants, plateaus, low_temperature,
          condensed_phase_limit, throat_scan, retention_threshold]

#: The one kind sampled in full (tests/Fixtures/BOOT.md, "The outputs are bound to the generator"): the throat
#: family's mass-flux search is the fixture kind the second hidden-defect audit's own performance node work turned
#: on, so every one of its cases is checked, not just a sample of them.
SAMPLE_IN_FULL = "throat"


def build_sample(only: set[str] | None) -> set[tuple[str, str]]:
    """At least one case of every kind under `tests/Fixtures/cases/` (or every case of `SAMPLE_IN_FULL`), read from
    the committed directory listing rather than typed by hand, restricted to `only` when it is given. A kind whose
    directory is empty or missing contributes nothing (it has no committed case to sample)."""
    sample: set[tuple[str, str]] = set()
    if not os.path.isdir(CASES):
        return sample
    for kind in sorted(os.listdir(CASES)):
        if only is not None and kind not in only:
            continue
        kind_dir = os.path.join(CASES, kind)
        if not os.path.isdir(kind_dir):
            continue
        names = sorted(file[:-len(".json")] for file in os.listdir(kind_dir) if file.endswith(".json"))
        if not names:
            continue
        picked = names if kind == SAMPLE_IN_FULL else names[:1]
        sample.update((kind, name) for name in picked)
    return sample


def main() -> int:
    args = sys.argv[1:]
    check = "--check" in args
    sample = "--sample" in args
    only = [a for a in args if not a.startswith("--")]
    only_cases = build_sample(set(only) if only else None) if sample else None
    writer = Writer(check=check, only=only or None, only_cases=only_cases)
    for script in SCRIPTS:
        script.generate(writer)
    return writer.finish()


if __name__ == "__main__":
    sys.exit(main())
