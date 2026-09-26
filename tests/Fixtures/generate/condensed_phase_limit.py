"""The hidden-defect audit's 17-element stress case (finding 2, 2026-09-26): enough condensed-forming metals for
a state to hold more than the prior 8-slot limit of condensed species at once (Equilibrium BOOT.md,
ScratchLayout.MaxCondensedInSolution).

The composition is not a propellant: seventeen elements built from `Custom` pure-element reactants, five of them
(H, O, C, N, Cl) at ordinary levels and twelve metals at a trace 1e-4 kmol/kg each, so that every metal's oxide
or chloride is a stable condensed phase at once. cea 3.3.4 solves it and reports all twelve condensed phases
together, which the prior 8-slot table could not represent.
"""
from __future__ import annotations

import sys

import numpy as np

from common import atomic_weight, read_thermo
from cea_cases import Custom, describe_reactants, equilibrium_inputs, make_mixtures, solve_equilibrium
from writer import Writer, main_of

RECORDS = read_thermo()

ELEMENTS = ["H", "O", "C", "N", "CL", "AL", "MG", "K", "NA", "CA", "TI", "ZR", "SR", "BA", "BE", "CU", "LI"]

# Per kilogram of the reactant blend: H, O, C, N and Cl at ordinary levels, then the twelve condensed-forming
# metals at a trace 1e-4 kmol/kg each (the audit's own matrix).
ELEMENT_MOLES = [0.02, 0.04, 0.005, 0.01, 0.001] + [1.0e-4] * 12

PRODUCTS = [
    "H2O", "CO2", "N2", "O2", "HCL", "CL2", "AL", "Mg", "K", "Na", "Ca", "Ti", "Zr", "Sr", "Ba", "Be", "Cu", "Li",
    "KCL", "NaCL", "H2", "CO", "O", "H", "OH", "NO",
    "AL2O3(a)", "MgO(cr)", "CaO(cr)", "TiO2(cr)", "ZrO2(III)", "SrO(cr)", "BaO(cr)", "BeO(a)", "CuO(cr)",
    "Li2O(cr)", "KCL(cr)", "NaCL(cr)",
]

PRESSURE_PA = 1.0e5
TEMPERATURE_K = 350.0


def generate(writer: Writer) -> None:
    reactants = [Custom(name=f"pure-{element}", formula={element: 1.0}, enthalpy_cal_per_mol=0.0) for element in ELEMENTS]
    weights = np.array([moles * atomic_weight(RECORDS, element) for element, moles in zip(ELEMENTS, ELEMENT_MOLES)])
    reac, prod = make_mixtures(reactants, PRODUCTS)
    descriptions = describe_reactants(reactants, weights)
    outputs = solve_equilibrium(reac, prod, weights, "tp", TEMPERATURE_K, PRESSURE_PA, transport=False)
    writer.case(
        "tp", f"seventeen-elements-many-condensed-phases_T{TEMPERATURE_K:g}",
        inputs=equilibrium_inputs(descriptions, PRODUCTS, "tp", TEMPERATURE_K, PRESSURE_PA, False, only=PRODUCTS),
        outputs=outputs, script_path=__file__)


if __name__ == "__main__":
    sys.exit(main_of(generate))
