"""The second hidden-defect audit's finding F1 (2026-09-28, Equilibrium BOOT.md, the two-stage retention
threshold): tp fixtures at conditions where a trace gaseous carrier sits on the single, wrong threshold of
1e-8 and the reference's own two-stage rule (tsize/xsize) does not.

RP-1311 example 5's table (ammonium perchlorate propellant) at 300 K, 1 bar and 70 bar, and at 305 K, 1 MPa:
the audit's own example of the crossing failure ("RP-1311 example 5 at 300 K, 1 bar").

A NaClO4 decomposition and AP/HTPB/Al at 420-430 K were also asked for (the orchestrator's task) and, that day,
were tried and recorded as not converging by the equilibrium node of that commit, so they were not committed
(the note this paragraph replaces named the two gaps: an all-gaseous cold start that never converges for the
salts, and a stood-down candidate the exit guard would not let go of for AP/HTPB/Al). Investigation 6
(2026-09-28) designed rules A and B for exactly these two gaps (Equilibrium BOOT.md, "Two rules come before
the remedies above"), and with them in place the reference's own four states converge on the tree too; the
cases are added below.

NaClO4 and KClO4, Na (or K) : Cl : O = 1 : 1 : 4, fed as three pure-element `Custom` reactants at the salt's
own mole ratio (there is no thermo.inp record for either salt), at 500 K and 800 K, 1 bar.

AP/HTPB/Al at 7 MPa/430 K and 1 MPa/420 K, on the same reactants, mass fractions and product table as the
`ap-htpb-al` chamber fixture of `propellants.py` (imported from there, never copied): a direct tp solve, not a
rocket station.

The threshold flip (2026-10-03, the orchestrator's investigation B2 for 0.2.1, Newton BOOT.md): the same salts at
the states where the trace carriers of the direction pi_K - pi_Cl (K and KO against CL) sat within one e-fold of
the first stage's threshold and the tree swapped them every step until the cap: KClO4 at 1150 K, 1 bar and at
1200 K, 10 bar, NaClO4 at 1120 K, 1 bar. A fixture whose pressure is not 1 bar carries it in its name; the
names of the 1 bar fixtures above are unchanged.
"""
from __future__ import annotations

import sys

import numpy as np

from cea_cases import Custom, describe_reactants, equilibrium_inputs, make_mixtures, solve_equilibrium
from common import atomic_weight, read_thermo
from propellants import PROPELLANTS
from rp1311 import EXAMPLE5_OMIT, EXAMPLE5_REACTANTS, EXAMPLE5_WEIGHTS
from writer import Writer, main_of

RECORDS = read_thermo()

BAR_TO_PA = 1.0e5
MPA_TO_PA = 1.0e6

# (temperature K, pressure Pa) pairs of RP-1311 example 5's table.
EXAMPLE5_STATES = [(300.0, 1.0 * BAR_TO_PA), (300.0, 70.0 * BAR_TO_PA), (305.0, 1.0 * MPA_TO_PA)]


def _rp1311_example5(writer: Writer) -> None:
    reac, prod = make_mixtures(EXAMPLE5_REACTANTS, omit=EXAMPLE5_OMIT)
    descriptions = describe_reactants(EXAMPLE5_REACTANTS, EXAMPLE5_WEIGHTS, 298.15)
    for temperature, pressure_pa in EXAMPLE5_STATES:
        outputs = solve_equilibrium(reac, prod, EXAMPLE5_WEIGHTS, "tp", temperature, pressure_pa, transport=False)
        writer.case(
            "tp", f"rp1311-example5_T{temperature:g}_p{pressure_pa / BAR_TO_PA:g}bar",
            inputs=equilibrium_inputs(descriptions, prod.species_names, "tp", temperature, pressure_pa, False,
                                      omit=EXAMPLE5_OMIT),
            outputs=outputs, script_path=__file__)


# Na (or K) : Cl : O = 1 : 1 : 4, the salt's own mole ratio.
SALT_RATIO = [1.0, 1.0, 4.0]
SALTS = [("naclo4", ["NA", "CL", "O"]), ("kclo4", ["K", "CL", "O"])]
SALT_STATES = [(500.0, BAR_TO_PA), (800.0, BAR_TO_PA)]

# The threshold flip: (salt, temperature K, pressure Pa), at the salt's own mole ratio.
FLIP_STATES = [("kclo4", 1150.0, BAR_TO_PA), ("naclo4", 1120.0, BAR_TO_PA), ("kclo4", 1200.0, 10.0 * BAR_TO_PA)]


def _pure_element_reactants(elements: list[str], ratio: list[float]) -> tuple[list[Custom], np.ndarray]:
    """One pure-element `Custom` reactant per element (zero assigned enthalpy, the element's own standard state),
    weighted so their mass fractions, once `describe_reactants` normalizes them, reproduce the formula's mole
    ratio: w_e = ratio_e * atomic_weight(e)."""
    reacs = [Custom(f"pure-{element}", {element: 1.0}, enthalpy_cal_per_mol=0.0) for element in elements]
    weights = np.array([r * atomic_weight(RECORDS, e) for e, r in zip(elements, ratio)])
    return reacs, weights


def _salt_case_name(name: str, temperature: float, pressure_pa: float) -> str:
    """`{name}_T{T}` at 1 bar, as the first fixtures of the family were named; any other pressure is in the name."""
    if pressure_pa == BAR_TO_PA:
        return f"{name}_T{temperature:g}"
    return f"{name}_T{temperature:g}_p{pressure_pa / BAR_TO_PA:g}bar"


def _salt(writer: Writer, name: str, elements: list[str], ratio: list[float],
          states: list[tuple[float, float]]) -> None:
    reacs, weights = _pure_element_reactants(elements, ratio)
    reac, prod = make_mixtures(reacs)
    descriptions = describe_reactants(reacs, weights)
    for temperature, pressure_pa in states:
        outputs = solve_equilibrium(reac, prod, weights, "tp", temperature, pressure_pa, transport=False)
        writer.case(
            "tp", _salt_case_name(name, temperature, pressure_pa),
            inputs=equilibrium_inputs(descriptions, prod.species_names, "tp", temperature, pressure_pa, False),
            outputs=outputs, script_path=__file__)


AP_HTPB_AL = next(p for p in PROPELLANTS if p["name"] == "ap-htpb-al")
AP_HTPB_AL_STATES = [(430.0, 7.0 * MPA_TO_PA), (420.0, 1.0 * MPA_TO_PA)]


def _ap_htpb_al(writer: Writer) -> None:
    reac, prod = make_mixtures(AP_HTPB_AL["reactants"])
    weights = np.array(AP_HTPB_AL["massFractions"])
    temperatures = np.array(AP_HTPB_AL["temperatures"])
    descriptions = describe_reactants(AP_HTPB_AL["reactants"], weights, temperatures)
    for temperature, pressure_pa in AP_HTPB_AL_STATES:
        outputs = solve_equilibrium(reac, prod, weights, "tp", temperature, pressure_pa, transport=False)
        writer.case(
            "tp", f"ap-htpb-al_pc{pressure_pa / MPA_TO_PA:g}MPa_T{temperature:g}",
            inputs=equilibrium_inputs(descriptions, prod.species_names, "tp", temperature, pressure_pa, False),
            outputs=outputs, script_path=__file__)


def generate(writer: Writer) -> None:
    _rp1311_example5(writer)
    for name, elements in SALTS:
        flips = [(t, p) for salt, t, p in FLIP_STATES if salt == name]
        _salt(writer, name, elements, SALT_RATIO, SALT_STATES + flips)
    _ap_htpb_al(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
