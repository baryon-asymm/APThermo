"""Equilibrium cases below the standard interval bounds: the hidden-defect audit's finding 1 (the Thermo node's
RecordLow rule, fixed to the reference's lowest-lower/highest-upper bound) and the Equilibrium node's open-below
rule for a condensed record whose data start at the gas data floor (both BOOT.md, 2026-09-26).

Reactants are plain database names (Si, Li, Ar, H2, O2); nothing here computes an expectation, the package
solves every case and the fixture carries its output as is.
"""
from __future__ import annotations

import sys

import numpy as np

from common import atomic_weight, read_thermo
from cea_cases import describe_reactants, equilibrium_inputs, make_mixtures, solve_equilibrium
from writer import Writer, main_of

RECORDS = read_thermo()

# The nine condensed records whose first, inverted interval runs 300 -> 298.15 K admit the phase from 298.15 K
# on under the reference's rule; these five temperatures straddle the old, wrong bound at 300 K.
RECORD_LOW_TEMPERATURES = [298.15, 299.0, 299.99, 300.0, 301.0]

# H2O(cr)'s data start at the gas data floor (200 K); cea 3.3.4 still holds ice below it (Equilibrium BOOT.md,
# open below). 199 K is one kelvin under the floor; the others go further down.
ICE_TEMPERATURES = [165.0, 180.0, 190.0, 199.0]

PRESSURE_PA = 1.0e5

ARGON_MASS_FRACTION = 0.02
TRACE_MASS_FRACTION = 1.0e-3


def _trace_in_argon(writer: Writer, element: str, products: list[str]) -> None:
    """A trace of `element` in argon, tp at the five temperatures around 300 K: the element's own condensed
    record (`Si(cr)`, `Li(cr)`) begins with the inverted interval of finding 1."""
    reactants = [element, "Ar"]
    weights = np.array([TRACE_MASS_FRACTION * atomic_weight(RECORDS, element),
                        ARGON_MASS_FRACTION * atomic_weight(RECORDS, "Ar")])
    reac, prod = make_mixtures(reactants, products)
    descriptions = describe_reactants(reactants, weights)
    for t in RECORD_LOW_TEMPERATURES:
        outputs = solve_equilibrium(reac, prod, weights, "tp", t, PRESSURE_PA, transport=False)
        writer.case(
            "tp", f"{element.lower()}-in-argon_T{t:g}",
            inputs=equilibrium_inputs(descriptions, products, "tp", t, PRESSURE_PA, False, only=products),
            outputs=outputs, script_path=__file__)


def _ice_below_the_gas_data_floor(writer: Writer) -> None:
    """H2/O2 at O/F 4 by mass, tp below 200 K: oxygen is the limiting reactant, so its hydrogen forms water
    that freezes solid at these temperatures, with the hydrogen excess left as gas."""
    reactants = ["H2", "O2"]
    products = ["H2", "O2", "H2O", "H2O(cr)", "H2O(L)"]
    weights = np.array([1.0, 4.0])
    reac, prod = make_mixtures(reactants, products)
    descriptions = describe_reactants(reactants, weights)
    for t in ICE_TEMPERATURES:
        outputs = solve_equilibrium(reac, prod, weights, "tp", t, PRESSURE_PA, transport=False)
        writer.case(
            "tp", f"h2-o2-of4_T{t:g}",
            inputs=equilibrium_inputs(descriptions, products, "tp", t, PRESSURE_PA, False, only=products),
            outputs=outputs, script_path=__file__)


def generate(writer: Writer) -> None:
    _trace_in_argon(writer, "Si", ["Si", "Si2", "Si3", "Ar", "Si(cr)", "Si(L)"])
    _trace_in_argon(writer, "Li", ["Li", "Li2", "Ar", "Li(cr)", "Li(L)"])
    _ice_below_the_gas_data_floor(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
