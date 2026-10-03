"""The three-element tie family (2026-10-03, Equilibrium's Newton node, BOOT.md, "Rule A: an element tie"): tp cases on
the element moles and the product lists of RP-1311 examples 1 (chemical equivalence ratio 1.0) and 12, at 300 K and
600 K, where only CO2, H2O and N2 (and Ar) carry carbon, hydrogen and oxygen and the oxygen row of the element matrix
equals 2 C + 1/2 H on them. A pair of elements in one ratio cannot express that tie; the equilibrium node ended these
states `SingularMatrix` before the rule was generalized to a linear combination of rows.

Four cases: example 1 at 300 K and 1 atm, and at 600 K and 0.01 atm; example 12 at 300 K and its chamber pressure of
1000 psi (`H2O(L)` and an `O2` carrier beside the gas), and at 600 K and 10 psi (gas only).

The mixtures are fed as **pure-element** `Custom` reactants at the example's own recorded element moles, with the
generator's atomic weights as their molar masses. Fed with the example's own reactants (H2 and air), the package reads
an element abundance whose cancellation `b_O - 2 b_C - b_H / 2` differs from the recorded one, which the committed
fixtures carry as `elementMoles`: the reference then came out lean where the recorded mixture is rich, and the mole
fraction of H2O stood 6.2e-6 apart, over the 5e-6 of the tolerance table. Pure elements at the recorded moles make the
reference and the package solve one and the same mixture.
"""
from __future__ import annotations

import sys

import cea
import numpy as np

from cea_cases import RECORDS, Custom, describe_reactants, equilibrium_inputs, make_mixtures, solve_equilibrium
from common import BAR_TO_PA, atomic_weight, element_moles
from rp1311 import EXAMPLE1_PRODUCTS, EXAMPLE12_PRODUCTS
from writer import Writer, main_of

#: The chemical equivalence ratio and the oxidizer-to-fuel ratio of the two examples' own mixtures.
EXAMPLE1_EQUIVALENCE_RATIO = 1.0
EXAMPLE12_OF_RATIO = 2.5

#: (temperature K, pressure) of each case: atmospheres for example 1, pounds per square inch for example 12.
EXAMPLE1_STATES = [(300.0, 1.0), (600.0, 0.01)]
EXAMPLE12_STATES = [(300.0, 1000.0), (600.0, 10.0)]


def _example1_mixture() -> list[dict]:
    """The descriptions of example 1's mixture at its equivalence ratio, whose element moles the cases record."""
    reactants = ["H2", "Air"]
    reac, _ = make_mixtures(reactants, products=EXAMPLE1_PRODUCTS)
    fuel_weights = reac.moles_to_weights(np.array([1.0, 0.0]))
    oxidant_weights = reac.moles_to_weights(np.array([0.0, 1.0]))
    of_ratio = reac.chem_eq_ratio_to_of_ratio(oxidant_weights, fuel_weights, EXAMPLE1_EQUIVALENCE_RATIO)
    weights = reac.of_ratio_to_weights(oxidant_weights, fuel_weights, of_ratio)
    return describe_reactants(reactants, weights, oxidizer=oxidant_weights, fuel=fuel_weights)


def _example12_mixture() -> list[dict]:
    """The same for example 12's mixture of monomethylhydrazine and nitrogen tetroxide."""
    reactants = ["CH6N2(L)", "N2O4(L)"]
    reac, _ = make_mixtures(reactants, products=EXAMPLE12_PRODUCTS)
    oxidizer, fuel = np.array([0.0, 1.0]), np.array([1.0, 0.0])
    weights = reac.of_ratio_to_weights(oxidizer, fuel, EXAMPLE12_OF_RATIO)
    return describe_reactants(reactants, weights, np.array([298.15, 298.15]), oxidizer=oxidizer, fuel=fuel)


def _write(writer: Writer, descriptions: list[dict], products: list[str], name: str,
           states: list[tuple[float, float]], pressure_of) -> None:
    """One tp case per state of one example, over the example's mixture as pure elements at its recorded moles."""
    moles = element_moles(RECORDS, descriptions)
    reactants = [Custom(f"pure-{element}", {element: 1.0}, enthalpy_cal_per_mol=0.0) for element in moles]
    weights = np.array([moles[element] * atomic_weight(RECORDS, element) for element in moles])
    reac, prod = make_mixtures(reactants, products=products)
    pure = describe_reactants(reactants, weights)
    for temperature, amount in states:
        pressure_pa = pressure_of(amount)
        outputs = solve_equilibrium(reac, prod, weights, "tp", temperature, pressure_pa, transport=False)
        writer.case(
            "tp", name.format(amount=amount, temperature=temperature),
            inputs=equilibrium_inputs(pure, prod.species_names, "tp", temperature, pressure_pa, False,
                                      only=products),
            outputs=outputs, script_path=__file__)


def generate(writer: Writer) -> None:
    _write(writer, _example1_mixture(), EXAMPLE1_PRODUCTS,
           "three-element_rp1311-example1_r1.0_p{amount:g}atm_T{temperature:g}", EXAMPLE1_STATES,
           lambda atm: cea.units.atm_to_bar(atm) * BAR_TO_PA)
    _write(writer, _example12_mixture(), EXAMPLE12_PRODUCTS,
           "three-element_rp1311-example12_p{amount:g}psi_T{temperature:g}", EXAMPLE12_STATES,
           lambda psi: cea.units.psi_to_bar(psi) * BAR_TO_PA)


if __name__ == "__main__":
    sys.exit(main_of(generate))
