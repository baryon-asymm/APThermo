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
nearest interval's polynomial is used and the point is flagged out of range.
"""
from __future__ import annotations

import math
import sys

from common import Record, read_thermo
from writer import Writer, main_of

SPECIES = [
    "H2O", "CO2", "H2", "N2", "O2", "H", "O", "OH", "CO", "N", "NO", "Ar", "HCL", "CL", "CL2",
    "AL", "ALCL", "AL2O", "ALO", "ALOH", "HF", "F", "CH4", "NH3", "C", "e-",
    "AL2O3(a)", "AL2O3(L)", "C(gr)", "H2O(L)", "H2O(cr)", "MgO(cr)", "ALCL3(cr)", "AL(cr)", "AL(L)",
    "Fe(a)", "Fe(L)", "W(cr)",
]

TEMPERATURES = [200.0, 298.15, 500.0, 1000.0, 1000.0001, 2000.0, 3000.0, 5000.0, 6000.0]
OUT_OF_RANGE_FACTOR = 0.05   # one point 5 % below the first bound and one 5 % above the last


def interval_of(record: Record, temperature: float) -> int:
    for k, interval in enumerate(record.intervals):
        if temperature <= interval.t_high:
            return k
    return len(record.intervals) - 1


def in_range(record: Record, temperature: float) -> bool:
    return record.intervals[0].t_low <= temperature <= record.intervals[-1].t_high


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
    first, last = record.intervals[0].t_low, record.intervals[-1].t_high
    inside = [t for t in TEMPERATURES if first <= t <= last]
    below = first * (1.0 - OUT_OF_RANGE_FACTOR)
    above = last * (1.0 + OUT_OF_RANGE_FACTOR)
    return [below] + inside + [above]


def generate(writer: Writer) -> None:
    records = read_thermo()
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
