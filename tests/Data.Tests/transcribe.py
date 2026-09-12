"""Independent transcription of selected records of data/thermo.inp and data/trans.inp into JSON fixtures.

This is not the loader under test: it reads the same files with token- and regex-based
rules where the loader uses fixed columns, so that the two agree only when both are right.
Run from anywhere: python tests/Data.Tests/records/transcribe.py
"""
from __future__ import annotations

import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
THERMO = os.path.join(ROOT, "data", "thermo.inp")
TRANS = os.path.join(ROOT, "data", "trans.inp")

SPECIES = ["H2O", "AL2O3(a)", "AL(cr)", "C(gr)", "e-", "O2(L)", "H2(L)", "RP-1", "N2O4(L)", "NH4CLO4(I)", "C2H8N2(L),UDMH"]
TRANSPORT_SINGLE = ["H2"]
TRANSPORT_PAIRS = [("CO", "CO2")]

NUMBER = re.compile(r"[+-]?\d*\.\d+[DE][ +-]?\d\d|[+-]?\d+\.\d*(?:[DE][ +-]?\d\d)?")
FORMULA_PAIR = re.compile(r"([A-Za-z][A-Za-z]?)\s*(\d+\.\d\d)")


def fnum(text: str) -> float:
    t = text.strip().replace("D", "E").replace("d", "E")
    t = re.sub(r"E\s+(\d)", r"E+\1", t)
    return float(t)


def find_record(lines: list[str], name: str) -> int:
    for i, line in enumerate(lines):
        if line[:18].strip() == name and line[:1] not in (" ", "!"):
            return i
    raise KeyError(name)


def transcribe_species(lines: list[str], end_products: int, name: str) -> dict:
    i = find_record(lines, name)
    l1, l2 = lines[i], lines[i + 1]
    n = int(l2[:2])
    formula = [{"symbol": s, "count": float(c)} for s, c in FORMULA_PAIR.findall(l2[10:50])]
    phase = "Gas" if int(l2[50:52]) == 0 else "Condensed"
    tail_tokens = l2[52:].split()
    molar_mass, hf = float(tail_tokens[0]), float(tail_tokens[1])
    intervals = []
    assigned_t = 0.0
    if n == 0:
        assigned_t = float(lines[i + 2].split()[0])
    else:
        j = i + 2
        for _ in range(n):
            h = lines[j]
            t_low, t_high = float(h[:11]), float(h[11:22])
            exps = [float(x) for x in h[23:63].split()]
            h_offset = float(h.split()[-1])
            coeffs = [fnum(x) for x in NUMBER.findall(lines[j + 1] + " " + lines[j + 2])]
            intervals.append({
                "tLow": t_low, "tHigh": t_high, "exponents": exps,
                "coefficients": coeffs[:7], "b1": coeffs[7], "b2": coeffs[8], "enthalpyOffset": h_offset,
            })
            j += 3
    return {
        "name": name,
        "line": i + 1,
        "comment": l1[18:].strip(),
        "dateCode": l2[3:9].strip(),
        "formula": formula,
        "phase": phase,
        "molarMass": molar_mass,
        "formationEnthalpy": hf,
        "assignedTemperature": assigned_t,
        "intervals": intervals,
        "section": "Products" if i < end_products else "Reactants",
        "isInert": name.startswith("Inert"),
    }


def transcribe_transport(lines: list[str], species: str, partner: str | None) -> dict:
    for i, line in enumerate(lines[1:], start=1):
        if line[:1] == " " or not line.strip():
            continue
        tokens = line.split()
        names = [t for t in tokens if not re.fullmatch(r"V\dC\d", t)]
        code_index = next(k for k, t in enumerate(tokens) if re.fullmatch(r"V\dC\d", t))
        names = tokens[:code_index]
        if partner is None and names == [species] or partner is not None and names == [species, partner]:
            code = tokens[code_index]
            nv, nc = int(code[1]), int(code[3])
            fits = {"V": [], "C": []}
            for k in range(nv + nc):
                fl = lines[i + 1 + k]
                kind = fl[1]
                nums = [fnum(x) for x in NUMBER.findall(fl[2:])]
                fits[kind].append({"tLow": nums[0], "tHigh": nums[1], "a": nums[2], "b": nums[3], "c": nums[4], "d": nums[5]})
            code_at = line.index(code)
            return {
                "species": species, "partner": partner, "line": i + 1,
                "reference": line[code_at + len(code):].strip(),  # verbatim, inner blanks kept
                "viscosity": fits["V"], "conductivity": fits["C"],
            }
    raise KeyError((species, partner))


def main() -> int:
    with open(THERMO, encoding="latin-1") as f:
        thermo = [l.rstrip("\n").rstrip("\r") for l in f]
    end_products = next(i for i, l in enumerate(thermo) if l.startswith("END PRODUCTS"))
    with open(TRANS, encoding="latin-1") as f:
        trans = [l.rstrip("\n").rstrip("\r") for l in f]
    out_dir = os.path.join(HERE, "records", "species")
    os.makedirs(out_dir, exist_ok=True)
    for name in SPECIES:
        record = transcribe_species(thermo, end_products, name)
        safe = re.sub(r"[^A-Za-z0-9_.-]", "_", name)
        with open(os.path.join(out_dir, safe + ".json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(record, f, indent=2)
    out_dir = os.path.join(HERE, "records", "transport")
    os.makedirs(out_dir, exist_ok=True)
    for name in TRANSPORT_SINGLE:
        with open(os.path.join(out_dir, name + ".json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(transcribe_transport(trans, name, None), f, indent=2)
    for a, b in TRANSPORT_PAIRS:
        with open(os.path.join(out_dir, f"{a}__{b}.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(transcribe_transport(trans, a, b), f, indent=2)
    print("fixtures written under", os.path.join(HERE, "records"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
