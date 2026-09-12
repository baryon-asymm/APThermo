"""Shared helpers of the fixture generator.

Repository paths, an independent reader of the NASA files (used by the scripts that do
not go through the reference package), element moles per kilogram from reactant
formulas, and the unit factors that turn the package's units into SI.

The reader here is deliberately not the tree's Data node and not the package: the
`thermo` and `transport` fixture kinds must be produced by code that shares nothing
with the code under test.
"""
from __future__ import annotations

import hashlib
import os
import re
from dataclasses import dataclass, field

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DATA = os.path.join(ROOT, "data")
CASES = os.path.abspath(os.path.join(HERE, "..", "cases"))
THERMO_INP = os.path.join(DATA, "thermo.inp")
TRANS_INP = os.path.join(DATA, "trans.inp")

# Units of the reference package's outputs, and the factors to SI.
BAR_TO_PA = 1.0e5                       # pressures: bar
KJ_TO_J = 1.0e3                         # enthalpy, entropy, heat capacities: kJ/kg, kJ/(kg K)
MILLIPOISE_TO_PA_S = 1.0e-4             # mixture viscosity: millipoise (1 P = 0.1 Pa s)
MW_PER_CM_K_TO_W_PER_M_K = 0.1          # mixture conductivity: mW/(cm K)
MICROPOISE_TO_PA_S = 1.0e-7             # trans.inp fits: micropoise
MICROW_PER_CM_K_TO_W_PER_M_K = 1.0e-4   # trans.inp fits: microwatt per (cm K)

_NUMBER_FIX_BLANK_SIGN = re.compile(r"([EDed])\s+(\d)")
_NUMBER_FIX_EMBEDDED_SIGN = re.compile(r"(\d)([+-]\d)")


def sha256_of(path: str) -> str:
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def safe_name(name: str) -> str:
    """A file-name-safe rendering of a case or species name."""
    return re.sub(r"[^A-Za-z0-9._-]", "_", name)


def fortran_float(text: str) -> float:
    t = text.strip()
    if not t:
        return 0.0
    t = t.replace("D", "E").replace("d", "E")
    t = _NUMBER_FIX_BLANK_SIGN.sub(r"\1+\2", t)
    if "E" not in t and "e" not in t:
        t = _NUMBER_FIX_EMBEDDED_SIGN.sub(r"\1E\2", t)
    return float(t)


@dataclass
class Interval:
    t_low: float
    t_high: float
    exponents: list[float]
    coefficients: list[float]   # a1 .. a7
    b1: float
    b2: float
    enthalpy_offset: float


@dataclass
class Record:
    name: str
    line: int
    formula: list[tuple[str, float]]
    condensed: bool
    molar_mass: float
    formation_enthalpy: float     # J/mol; the assigned enthalpy when there are no intervals
    section: str
    intervals: list[Interval] = field(default_factory=list)
    assigned_temperature: float = 0.0


def read_thermo(path: str = THERMO_INP) -> dict[str, Record]:
    """All records of thermo.inp by name; products first, so a reactant-only name never shadows a product."""
    with open(path, encoding="latin-1") as f:
        lines = [l.rstrip("\r\n") for l in f]
    records: dict[str, Record] = {}
    i = 0
    while not lines[i].lower().startswith("thermo"):
        i += 1
    i += 2  # the "thermo" line and the bounds line
    section = "Products"
    while i < len(lines):
        line = lines[i]
        if line.startswith("END PRODUCTS"):
            section = "Reactants"
            i += 1
            continue
        if line.startswith("END REACTANTS"):
            break
        if not line.strip() or line.lstrip().startswith("!"):
            i += 1
            continue
        name = line[:18].strip()
        l2 = lines[i + 1]
        n_intervals = int(l2[:2])
        formula = []
        for k in range(5):
            symbol = l2[10 + 8 * k:12 + 8 * k].strip()
            count = fortran_float(l2[12 + 8 * k:18 + 8 * k])
            if symbol:
                formula.append((symbol, count))
        condensed = int(l2[50:52]) != 0
        molar_mass = fortran_float(l2[52:65])
        hf = fortran_float(l2[65:80])
        record = Record(name, i + 1, formula, condensed, molar_mass, hf, section)
        if n_intervals == 0:
            record.assigned_temperature = fortran_float(lines[i + 2].split()[0])
            i += 3
        else:
            i += 2
            for _ in range(n_intervals):
                h = lines[i]
                exps = [fortran_float(h[23 + 5 * k:28 + 5 * k]) for k in range(8)]
                c1, c2 = lines[i + 1], lines[i + 2]
                coeffs = [fortran_float(c1[16 * k:16 * k + 16]) for k in range(5)]
                coeffs += [fortran_float(c2[0:16]), fortran_float(c2[16:32])]
                record.intervals.append(Interval(
                    fortran_float(h[0:11]), fortran_float(h[11:22]), exps, coeffs,
                    fortran_float(c2[48:64]), fortran_float(c2[64:80]), fortran_float(h[65:80])))
                i += 3
        records.setdefault(name, record)
    return records


@dataclass
class TransportFit:
    kind: str       # "V" or "C"
    t_low: float
    t_high: float
    a: float
    b: float
    c: float
    d: float


@dataclass
class TransportBlock:
    species: str
    partner: str | None
    reference: str
    fits: list[TransportFit]


def read_trans(path: str = TRANS_INP) -> list[TransportBlock]:
    with open(path, encoding="latin-1") as f:
        lines = [l.rstrip("\r\n") for l in f]
    blocks: list[TransportBlock] = []
    i = 1
    code = re.compile(r"V(\d)C(\d)")
    while i < len(lines):
        line = lines[i]
        if line.lower().startswith("end"):
            break
        m = code.search(line, 32)
        if line[:1] == " " or not m:
            i += 1
            continue
        species = line[:16].strip()
        partner = line[16:32].strip() or None
        count = int(m.group(1)) + int(m.group(2))
        fits = []
        for k in range(count):
            fl = lines[i + 1 + k]
            fits.append(TransportFit(fl[1], fortran_float(fl[2:11]), fortran_float(fl[11:20]),
                                     fortran_float(fl[20:35]), fortran_float(fl[35:50]),
                                     fortran_float(fl[50:65]), fortran_float(fl[65:80])))
        blocks.append(TransportBlock(species, partner, line[m.end():].strip(), fits))
        i += 1 + count
    return blocks


def atomic_weight(records: dict[str, Record], symbol: str) -> float:
    """The molar mass of the monatomic gaseous product species of the element (symbol compared case-insensitively)."""
    candidates = [r for r in records.values()
                  if r.section == "Products" and not r.condensed and len(r.formula) == 1
                  and r.formula[0][1] == 1.0 and r.formula[0][0].upper() == symbol.upper()]
    exact = [r for r in candidates if r.name.upper() == symbol.upper()]
    chosen = exact or candidates
    if not chosen:
        raise KeyError(symbol)
    return chosen[0].molar_mass


def element_moles(records: dict[str, Record], reactants: list[dict]) -> dict[str, float]:
    """Kilomoles of every element per kilogram of mixture: sum over reactants of w_r / M_r times the atom count.

    Each reactant is a dict with "massFraction" and either "name" (a thermo.inp record) or
    "formula" ({symbol: count}) with "molarMass".
    """
    moles: dict[str, float] = {}
    for r in reactants:
        w = r["massFraction"]
        if "formula" in r and "molarMass" in r:
            pairs = list(r["formula"].items())
            molar_mass = r["molarMass"]
        else:
            record = records[r["name"]]
            pairs = record.formula
            molar_mass = record.molar_mass
        for symbol, count in pairs:
            key = symbol.upper()
            moles[key] = moles.get(key, 0.0) + w / molar_mass * count
    return dict(sorted(moles.items()))
