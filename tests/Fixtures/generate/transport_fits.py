"""Independent evaluation of the pure-species and pair fits of data/trans.inp.

    ln(property) = A ln T + B / T + C / T^2 + D

Viscosity fits are in micropoise, conductivity fits in microwatt per (cm K); the fixture
carries SI values (Pa s, W/(m K)) with the factors named in `common.py`. Nothing here goes
through the reference package or the tree.
"""
from __future__ import annotations

import math
import sys

from common import MICROPOISE_TO_PA_S, MICROW_PER_CM_K_TO_W_PER_M_K, TransportBlock, read_trans
from writer import Writer, main_of

SPECIES = ["H2", "H2O", "O2", "N2", "CO", "CO2", "H", "O", "OH", "NO", "N", "Ar", "HCL", "CL2", "CH4", "NH3", "HF", "e-", "C", "N2O"]
PAIRS = [("H2", "H2O"), ("H2", "N2"), ("H2", "O2"), ("CO", "CO2"), ("H2O", "O2"), ("N2", "O2"), ("CO2", "O2")]
TEMPERATURES = [300.0, 500.0, 1000.0, 2000.0, 3000.0, 5000.0]


def fit_value(fit, temperature: float) -> float:
    return math.exp(fit.a * math.log(temperature) + fit.b / temperature + fit.c / temperature ** 2 + fit.d)


def points(fit) -> list[float]:
    inside = [t for t in TEMPERATURES if fit.t_low < t < fit.t_high]
    return [fit.t_low] + inside + [fit.t_high]


def evaluate(block: TransportBlock) -> dict:
    viscosity, conductivity = [], []
    for k, fit in enumerate(block.fits):
        factor = MICROPOISE_TO_PA_S if fit.kind == "V" else MICROW_PER_CM_K_TO_W_PER_M_K
        target = viscosity if fit.kind == "V" else conductivity
        for t in points(fit):
            target.append({"temperature": t, "fit": k, "value": fit_value(fit, t) * factor})
    return {"viscosity": viscosity, "conductivity": conductivity}


def generate(writer: Writer) -> None:
    blocks = read_trans()
    singles = {b.species: b for b in blocks if b.partner is None}
    pairs = {frozenset((b.species, b.partner)): b for b in blocks if b.partner is not None}
    common_inputs = {
        "fileUnits": {"viscosity": "micropoise", "conductivity": "microwatt per centimetre kelvin"},
        "factorsToSi": {"viscosity": MICROPOISE_TO_PA_S, "conductivity": MICROW_PER_CM_K_TO_W_PER_M_K},
    }
    for name in SPECIES:
        block = singles[name]
        writer.case(
            "transport", "fit_" + name,
            inputs={"species": name, "partner": None, "reference": block.reference,
                    "fits": [[f.kind, f.t_low, f.t_high] for f in block.fits], **common_inputs},
            outputs=evaluate(block),
            script_path=__file__,
            method="independent-evaluation",
        )
    for a, b in PAIRS:
        block = pairs[frozenset((a, b))]
        writer.case(
            "transport", f"fit_{a}__{b}",
            inputs={"species": block.species, "partner": block.partner, "reference": block.reference,
                    "fits": [[f.kind, f.t_low, f.t_high] for f in block.fits], **common_inputs},
            outputs=evaluate(block),
            script_path=__file__,
            method="independent-evaluation",
        )


if __name__ == "__main__":
    sys.exit(main_of(generate))
