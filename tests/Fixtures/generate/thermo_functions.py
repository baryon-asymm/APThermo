"""Independent evaluation of the NASA polynomials of data/thermo.inp: Cp/R, H/RT, S/R, G/RT.

Nothing here goes through the reference package or the tree: the records are read by the
generator's own reader and the formulas are written out in their general form,

    Cp/R  = sum_k a_k T^e_k
    H/RT  = sum_k a_k I_h(e_k, T) + b1 / T,   I_h(e, T) = T^e / (e + 1),  I_h(-1, T) = ln T / T
    S/R   = sum_k a_k I_s(e_k, T) + b2,       I_s(e, T) = T^e / e,        I_s(0, T) = ln T
    G/RT  = H/RT - S/R

with the eight exponents e_k taken from the record (the eighth is unused when zero and its
coefficient is absent). Interval selection follows the rule of the Thermo node: the first
interval whose upper bound is not below T; below the first bound or above the last one the
nearest interval's polynomial is used and the point is flagged out of range. A record's range
is the lowest lower and the highest upper bound over its intervals, bound by bound (the
reference's `minval(T_fit(:, 1))`/`maxval(T_fit(:, 2))`, cea 3.3.4 equilibrium.f90 1692-1693 and
1913-1915), not the first and last interval's own bounds: eleven condensed records of the
committed file begin with an interval that runs backwards (src/Thermo/HISTORY.md#record-bounds, 2026-09-26).
"""
from __future__ import annotations

import math
import os
import sys

from common import ROOT, Record, read_thermo_joined
from writer import Writer, main_of

# The Data node's approved anomaly list: condensed records whose first interval is not ascending
# (tests/Data.Tests/records/interval-anomalies.approved.txt). Read from that file, not typed, so the two lists
# cannot drift apart (src/Thermo/HISTORY.md#record-bounds, 2026-09-26).
ANOMALY_LIST_PATH = os.path.join(ROOT, "tests", "Data.Tests", "records", "interval-anomalies.approved.txt")


def anomaly_species() -> list[str]:
    with open(ANOMALY_LIST_PATH, encoding="utf-8") as f:
        return [line.split(" [", 1)[0] for line in f if line.strip()]


SPECIES = [
    "H2O", "CO2", "H2", "N2", "O2", "H", "O", "OH", "CO", "N", "NO", "Ar", "HCL", "CL", "CL2",
    "AL", "ALCL", "AL2O", "ALO", "ALOH", "HF", "F", "CH4", "NH3", "C", "e-",
    "AL2O3(a)", "AL2O3(L)", "C(gr)", "H2O(L)", "H2O(cr)", "MgO(cr)", "ALCL3(cr)", "AL(cr)", "AL(L)",
    "Fe(a)", "Fe(L)", "W(cr)",
    # Multi-record and multi-piece condensed species (the Thermo node's join-and-cut): Cr(cr) is two records
    # joined into one contiguous fit, ALN(L) one record whose fits disagree at 2700 K by a real latent heat.
    "Cr(cr)", "ALN(L)",
] + anomaly_species()

TEMPERATURES = [200.0, 298.15, 500.0, 1000.0, 1000.0001, 2000.0, 3000.0, 5000.0, 6000.0]
OUT_OF_RANGE_FACTOR = 0.05   # one point 5 % below the first bound and one 5 % above the last


def interval_of(record: Record, temperature: float) -> int:
    for k, interval in enumerate(record.intervals):
        if temperature <= interval.t_high:
            return k
    return len(record.intervals) - 1


def record_bounds(record: Record) -> tuple[float, float]:
    """The lowest lower and the highest upper bound over the record's intervals, bound by bound."""
    return min(iv.t_low for iv in record.intervals), max(iv.t_high for iv in record.intervals)


def in_range(record: Record, temperature: float) -> bool:
    low, high = record_bounds(record)
    return low <= temperature <= high


def functions(record: Record, temperature: float) -> tuple[int, float, float, float]:
    k = interval_of(record, temperature)
    iv = record.intervals[k]
    t = temperature
    cp = 0.0
    h = iv.b1 / t
    s = iv.b2
    for e, a in zip(iv.exponents, iv.coefficients):
        cp += a * t ** e
        h += a * (math.log(t) / t if e == -1.0 else t ** e / (e + 1.0))
        s += a * (math.log(t) if e == 0.0 else t ** e / e)
    return k, cp, h, s


def points(record: Record) -> list[float]:
    """The standard temperatures inside the record's range, the range's bounds and midpoint, and one point beyond each end."""
    first, last = record_bounds(record)
    inside = sorted({t for t in TEMPERATURES if first <= t <= last} | {first, 0.5 * (first + last), last})
    below = first * (1.0 - OUT_OF_RANGE_FACTOR)
    above = last * (1.0 + OUT_OF_RANGE_FACTOR)
    return [below] + inside + [above]


def generate(writer: Writer) -> None:
    records = read_thermo_joined()   # condensed records of one name concatenated, as the Thermo node's builder joins them
    for name in SPECIES:
        record = records[name]
        if not record.intervals:
            raise ValueError(f"{name} has no polynomial intervals")
        values = []
        for t in points(record):
            k, cp, h, s = functions(record, t)
            values.append({
                "temperature": t,
                "inRange": in_range(record, t),
                "interval": k,
                "cpOverR": cp,
                "hOverRT": h,
                "sOverR": s,
                "gOverRT": h - s,
            })
        writer.case(
            "thermo", name,
            inputs={
                "species": name,
                "line": record.line,
                "phase": "condensed" if record.condensed else "gas",
                "molarMass": record.molar_mass,
                "intervals": [[iv.t_low, iv.t_high] for iv in record.intervals],
                "exponents": [iv.exponents for iv in record.intervals],
                "temperatures": [v["temperature"] for v in values],
            },
            outputs={"values": values},
            script_path=__file__,
            method="independent-evaluation",
        )


if __name__ == "__main__":
    sys.exit(main_of(generate))
