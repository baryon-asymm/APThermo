"""Field-by-field comparison of a regenerated fixture document with the committed one.

The comparison of `regenerate.py --check --sample` (tests/Fixtures/BOOT.md, "The binding step runs on the fixtures'
own platform and compares with a tolerance"). Two documents agree when they have the same keys in the same order
and the same list lengths, every string, boolean and null is equal, every number is within the relative tolerance
of the `regeneration` row of `tolerances.json`, the numbers a mass-flux search derives (the `throat` kind's throat
station, the characteristic velocity of every station, the package's own throat) are within its `throat` row, and
of the provenance block the keys `generatedOn`, `thermoLibSha256` and `transLibSha256` are left out (the first is
the day of the run, the other two hash binaries the package builds per platform) while every other provenance
key is equal. Plain `regenerate.py --check` does not use this module: it compares exact text.
"""
from __future__ import annotations

import json
import os

from common import CASES

TOLERANCES_PATH = os.path.abspath(os.path.join(CASES, "..", "tolerances.json"))

#: The provenance keys whose values are not compared: the day of the run, and the hashes of two binaries the
#: package builds per platform.
PROVENANCE_LEFT_OUT = ("generatedOn", "thermoLibSha256", "transLibSha256")

#: The kind whose search-derived numbers take the `throat` row.
SEARCH_KIND = "throat"

#: The state key that carries the throat's characteristic velocity on every station, the chamber's included.
SEARCH_STATE_KEYS = ("characteristicVelocity",)


def _relative_of(row: str) -> float:
    """The relative tolerance of one row of `tolerances.json`, the only table of the tree."""
    with open(TOLERANCES_PATH, encoding="utf-8") as f:
        return float(json.load(f)["fields"][row]["relative"])


def _is_number(value) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _throat_stations(document: dict) -> set[int]:
    """The indexes of the stations named `throat` in a document of the `throat` kind."""
    stations = document.get("outputs", {}).get("stations", [])
    return {i for i, s in enumerate(stations) if isinstance(s, dict) and s.get("station") == "throat"}


class DocumentComparison:
    """Compares documents of one kind; reads the two relative tolerances once."""

    def __init__(self) -> None:
        self.regeneration = _relative_of("regeneration")
        self.throat = _relative_of("throat")

    def differences(self, expected: dict, actual: dict) -> list[str]:
        """The differences of `actual` (regenerated) from `expected` (committed), one line each, each naming the
        dotted path of the field; empty when the two agree."""
        found: list[str] = []
        kind = expected.get("case", {}).get("kind")
        throat_stations = _throat_stations(expected) if kind == SEARCH_KIND else set()
        self._walk((), expected, actual, kind, throat_stations, found)
        return found

    def _relative(self, path: tuple, kind: str | None, throat_stations: set[int]) -> float:
        if kind == SEARCH_KIND and path[:1] == ("outputs",):
            if path[1:2] == ("packageRocketThroat",):
                return self.throat
            if path[1:2] == ("stations",) and len(path) > 2:
                if path[2] in throat_stations or (len(path) == 4 and path[3] in SEARCH_STATE_KEYS):
                    return self.throat
        return self.regeneration

    def _walk(self, path: tuple, expected, actual, kind, throat_stations, found: list[str]) -> None:
        here = _dotted(path)
        if len(path) == 2 and path[0] == "generator" and path[1] in PROVENANCE_LEFT_OUT:
            return
        if isinstance(expected, dict):
            if not isinstance(actual, dict):
                found.append(f"{here}: expected an object, regenerated {_kind_of(actual)}")
                return
            if list(expected) != list(actual):
                found.append(f"{here}: keys differ, only committed {[k for k in expected if k not in actual]}, "
                             f"only regenerated {[k for k in actual if k not in expected]}"
                             + ("" if set(expected) != set(actual) else ", same keys in another order"))
                return
            for key in expected:
                self._walk(path + (key,), expected[key], actual[key], kind, throat_stations, found)
        elif isinstance(expected, list):
            if not isinstance(actual, list):
                found.append(f"{here}: expected a list, regenerated {_kind_of(actual)}")
                return
            if len(expected) != len(actual):
                found.append(f"{here}: length differs, committed {len(expected)}, regenerated {len(actual)}")
                return
            for i, (e, a) in enumerate(zip(expected, actual)):
                self._walk(path + (i,), e, a, kind, throat_stations, found)
        elif _is_number(expected) and _is_number(actual):
            relative = self._relative(path, kind, throat_stations)
            if abs(expected - actual) > relative * abs(expected):
                found.append(f"{here}: committed {expected!r}, regenerated {actual!r}, relative difference "
                             f"{_relative_difference(expected, actual):.3g} above {relative:g}")
        elif expected != actual or type(expected) is not type(actual):
            found.append(f"{here}: committed {expected!r}, regenerated {actual!r}")


def _relative_difference(expected: float, actual: float) -> float:
    return abs(expected - actual) / abs(expected) if expected != 0 else float("inf")


def _kind_of(value) -> str:
    return type(value).__name__


def _dotted(path: tuple) -> str:
    text = ""
    for part in path:
        text += f"[{part}]" if isinstance(part, int) else ("." if text else "") + str(part)
    return text or "(document)"
