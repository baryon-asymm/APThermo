"""Regenerates every fixture kind: python tests/Fixtures/generate/regenerate.py [--check] [--sample] [kind ...]

Without --check, fixtures whose content changed are rewritten (with today's date) and stale
files are removed; unchanged fixtures are left byte for byte. With --check nothing is written
and the exit code is 1 when any fixture would change, is missing or is stale.

--sample restricts every kind to a sample chosen from the committed directory listing
(tests/Fixtures/ACCEPTANCE.md, "The outputs are bound to the generator" and "The sample of the
binding step covers every script"): at least one case of every (script, kind) pair the
committed fixtures record, read from each file's own `generator.script` field, plus every
case of the `throat` kind. A script that writes several kinds (`propellants.py`'s rocket, hp,
sp and tp; `plateaus.py`'s rocket, hp and tp) contributes one case per kind it writes, not one
case per kind directory, so a script whose cases never sort first in a shared kind directory
is still sampled. Meant for --check on CI, where regenerating and comparing every fixture is
too slow for every push; it binds the outputs to the generator without paying that cost.
Combine with a `kind` filter to sample only those kinds. Plain --check compares exact text; --check --sample
compares each regenerated case with the committed file as a document, field by field, with the tolerances of
`tolerances.json` (document_comparison.py; tests/Fixtures/ACCEPTANCE.md, "The binding step runs on the fixtures' own
platform and compares with a tolerance"), so that a machine other than the one that wrote the files, the same
platform on another CPU, still binds the outputs to the generator.

After generation, --sample fails (exit 1) when the run compared nothing at all, or when a
(script, kind) pair the sample intended to cover produced no comparison: a script that no
longer writes the exact case name the sample picked from the committed listing is a real
gap, not something a silent, empty comparison should pass (tests/Fixtures/ACCEPTANCE.md, the third
audit pass's finding 4c).

Run it with the interpreter of the generator's environment (requirements.txt).
"""
from __future__ import annotations

import json
import os
import sys

import condensed_phase_limit
import constants
import low_temperature
import plateaus
import propellants
import retention_threshold
import rp1311
import seeded
import thermo_functions
import three_element
import throat_scan
import transport_fits
from common import CASES, safe_name
from writer import Writer

SCRIPTS = [constants, thermo_functions, transport_fits, rp1311, propellants, plateaus, low_temperature,
          condensed_phase_limit, throat_scan, retention_threshold, three_element, seeded]

#: The one kind sampled in full (tests/Fixtures/ACCEPTANCE.md, "The outputs are bound to the generator"): the throat
#: family's mass-flux search is the fixture kind the second hidden-defect audit's own performance node work turned
#: on, so every one of its cases is checked, not just a sample of them.
SAMPLE_IN_FULL = "throat"


def build_sample(only: set[str] | None) -> tuple[set[tuple[str, str]], dict[tuple[str, str], str]]:
    """At least one case of every (script, kind) pair the committed fixtures record (or every case of
    `SAMPLE_IN_FULL`), read from the committed directory listing rather than typed by hand, restricted to `only`
    when it is given. The script of a case is read from its own committed `generator.script` field, the same
    provenance field `Writer.case` writes, so a kind two or more scripts write (`hp`, `rocket`, `sp`, `tp`) samples
    each of those scripts once, not once for the kind as a whole (tests/Fixtures/ACCEPTANCE.md, the third audit pass's
    finding 4c). A kind whose directory is empty or missing contributes nothing (it has no committed case to
    sample). Returns the sample itself, for `Writer(only_cases=...)`, and the (script, kind) -> case name mapping
    the sample was built from, so the caller can verify every one of them was actually compared."""
    sample: set[tuple[str, str]] = set()
    required: dict[tuple[str, str], str] = {}
    if not os.path.isdir(CASES):
        return sample, required
    for kind in sorted(os.listdir(CASES)):
        if only is not None and kind not in only:
            continue
        kind_dir = os.path.join(CASES, kind)
        if not os.path.isdir(kind_dir):
            continue
        names = sorted(file[:-len(".json")] for file in os.listdir(kind_dir) if file.endswith(".json"))
        if not names:
            continue
        if kind == SAMPLE_IN_FULL:
            sample.update((kind, name) for name in names)
            for name in names:
                with open(os.path.join(kind_dir, name + ".json"), encoding="utf-8") as f:
                    script = json.load(f).get("generator", {}).get("script", "")
                required.setdefault((script, kind), name)
            continue
        for name in names:
            with open(os.path.join(kind_dir, name + ".json"), encoding="utf-8") as f:
                script = json.load(f).get("generator", {}).get("script", "")
            key = (script, kind)
            if key not in required:
                required[key] = name
                sample.add((kind, name))
    return sample, required


def check_sample_coverage(required: dict[tuple[str, str], str], produced: set[str]) -> list[str]:
    """The (script, kind) pairs `build_sample` intended to cover but nothing was actually compared for: the exact
    committed case name it picked never appeared in `Writer.produced`, because the script no longer writes that
    case (the case matrix moved under the sample) or because a future kind slips past this fact's own coverage.
    Also reports when `required` itself is empty, since that means the whole comparison compared nothing."""
    if not required:
        return ["the sample is empty; nothing was compared"]
    problems = []
    for (script, kind), name in sorted(required.items()):
        path = os.path.normcase(os.path.join(CASES, kind, safe_name(name) + ".json"))
        if path not in produced:
            problems.append(f"{script or '(unknown script)'} wrote no case compared for kind '{kind}' "
                             f"(expected {os.path.relpath(path, CASES)})")
    return problems


def main() -> int:
    args = sys.argv[1:]
    check = "--check" in args
    sample = "--sample" in args
    only = [a for a in args if not a.startswith("--")]
    only_cases, required = build_sample(set(only) if only else None) if sample else (None, None)
    writer = Writer(check=check, only=only or None, only_cases=only_cases, tolerant=sample)
    for script in SCRIPTS:
        script.generate(writer)
    exit_code = writer.finish()
    if sample:
        problems = check_sample_coverage(required, writer.produced)
        for problem in problems:
            print(f"sample coverage: {problem}")
        if problems:
            exit_code = 1
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
