"""Writes fixture documents with provenance, deterministically, and compares them with the committed ones.

A fixture is `{"case": {"name", "kind", "inputs"}, "generator": {provenance}, "outputs": {...}}`.
Two documents are "the same" when they differ at most in `generatedOn`; a document that is
the same as the committed one is left untouched, so that regeneration is byte-identical and
the date records when the content last changed.
"""
from __future__ import annotations

import datetime
import json
import os
import re
import sys

import numpy as np

import cea

from common import CASES, THERMO_INP, TRANS_INP, safe_name, sha256_of

_DATE = re.compile(r'"generatedOn": "\d{4}-\d{2}-\d{2}"')


def _json_default(value):
    if isinstance(value, (np.floating,)):
        return float(value)
    if isinstance(value, (np.integer,)):
        return int(value)
    if isinstance(value, (np.bool_,)):
        return bool(value)
    if isinstance(value, np.ndarray):
        return value.tolist()
    raise TypeError(f"not serializable: {type(value)!r}")


def dumps(document: dict) -> str:
    return json.dumps(document, indent=2, ensure_ascii=False, allow_nan=False, default=_json_default) + "\n"


class Writer:
    def __init__(self, check: bool = False, only: list[str] | None = None) -> None:
        self.check = check
        self.only = set(only) if only else None
        self.results: list[tuple[str, str]] = []
        self.produced: set[str] = set()
        self._provenance: dict[str, dict] = {}
        package_dir = os.path.dirname(cea.__file__)
        self._package_fields = {
            "package": "cea",
            "version": cea.__version__,
            "libraryVersion": cea.lib_version(),
            "thermoLibSha256": sha256_of(os.path.join(package_dir, "data", "thermo.lib")),
            "transLibSha256": sha256_of(os.path.join(package_dir, "data", "trans.lib")),
            "dataThermoSha256": sha256_of(THERMO_INP),
            "dataTransSha256": sha256_of(TRANS_INP),
        }

    def provenance(self, script_path: str, method: str) -> dict:
        key = script_path + "|" + method
        if key not in self._provenance:
            self._provenance[key] = {
                **self._package_fields,
                "method": method,
                "script": os.path.basename(script_path),
                "scriptSha256": sha256_of(script_path),
                "generatedOn": datetime.date.today().isoformat(),
            }
        return self._provenance[key]

    def wants(self, kind: str) -> bool:
        return self.only is None or kind in self.only

    def case(self, kind: str, name: str, inputs: dict, outputs: dict, script_path: str,
             method: str = "cea-package") -> str:
        """Writes (or, in check mode, compares) one fixture; returns unchanged, changed, missing or written."""
        if not self.wants(kind):
            return "skipped"
        document = {
            "case": {"name": name, "kind": kind, "inputs": inputs},
            "generator": self.provenance(script_path, method),
            "outputs": outputs,
        }
        path = os.path.join(CASES, kind, safe_name(name) + ".json")
        self.produced.add(os.path.normcase(path))
        new_text = dumps(document)
        if os.path.exists(path):
            with open(path, encoding="utf-8", newline="") as f:
                old_text = f.read()
            outcome = "unchanged" if _DATE.sub("", old_text) == _DATE.sub("", new_text) else "changed"
        else:
            outcome = "missing"
        if outcome != "unchanged" and not self.check:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(new_text)
            outcome = "written"
        self.results.append((os.path.relpath(path, CASES), outcome))
        return outcome

    def finish(self) -> int:
        """Reports stale files, prints a summary and returns the process exit code."""
        stale = []
        produced_kinds = {os.path.basename(os.path.dirname(p)) for p in self.produced}
        for kind in sorted(os.listdir(CASES)) if os.path.isdir(CASES) else []:
            kind_dir = os.path.join(CASES, kind)
            # Only the kinds this run produced are swept: a single script must not remove the others' files.
            if not os.path.isdir(kind_dir) or not self.wants(kind) or os.path.normcase(kind) not in produced_kinds:
                continue
            for file in sorted(os.listdir(kind_dir)):
                path = os.path.join(kind_dir, file)
                if file.endswith(".json") and os.path.normcase(path) not in self.produced:
                    stale.append(path)
        counts: dict[str, int] = {}
        for _, outcome in self.results:
            counts[outcome] = counts.get(outcome, 0) + 1
        for path, outcome in self.results:
            if outcome != "unchanged":
                print(f"{outcome:9s} {path}")
        for path in stale:
            if self.check:
                print(f"stale     {os.path.relpath(path, CASES)}")
            else:
                os.remove(path)
                print(f"removed   {os.path.relpath(path, CASES)}")
        print("fixtures:", ", ".join(f"{k} {v}" for k, v in sorted(counts.items())) or "none",
              f"; stale {len(stale)}" if stale else "")
        if self.check:
            bad = sum(v for k, v in counts.items() if k != "unchanged") + len(stale)
            return 1 if bad else 0
        return 0


def main_of(generate) -> int:
    """Runs one script standalone: `python <script>.py [--check] [--only kind ...]`."""
    args = sys.argv[1:]
    check = "--check" in args
    only = [a for a in args if not a.startswith("--")]
    writer = Writer(check=check, only=only or None)
    generate(writer)
    return writer.finish()
