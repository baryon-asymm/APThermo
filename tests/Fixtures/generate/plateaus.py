"""The melting-plateau cases of the case matrix: AP/HTPB/Al around the AL2O3(a)/AL2O3(L) transition.

Added by the design session of 2026-09-13 (the condensed-phase rules of the equilibrium node):
direct single-exit rocket cases across the plateau at 7 MPa, the fuel-rich hp chamber that
exercises the include/remove cycle, hp cases stepping through the latent-heat band at 700 kPa,
and a tp case at the transition bound itself. The rocket cases are single-exit, each solved
from the chamber, because the package's sequential multi-station path is exactly what the
station guard rejects near a plateau (BOOT.md).
"""
from __future__ import annotations

import sys

import numpy as np

import cea

from cea_cases import (FLOW_SHIFTING, RECORDS, describe_reactants, equilibrium_inputs, make_mixtures,
                       rocket_inputs, rocket_outputs, solve_equilibrium, solve_rocket)
from propellants import HTPB
from writer import Writer, main_of

MPA_TO_PA = 1.0e6

REACTANTS = ["NH4CLO4(I)", HTPB, "AL(cr)"]
TEMPERATURES = np.array([298.15, 298.15, 298.15])
MASS_FRACTIONS = np.array([0.68, 0.14, 0.18])

CHAMBER_PRESSURE_PA = 7.0 * MPA_TO_PA
# Pressure ratios covering the AL2O3(a)/AL2O3(L) plateau of this propellant at 7 MPa and both its edges.
PRESSURE_RATIOS = [21.6, 23.0, 25.0, 27.0, 30.0, 33.0, 36.0, 37.4]

BAND_PRESSURE_PA = 0.7 * MPA_TO_PA
BAND_FRACTIONS = [0.1, 0.3, 0.5, 0.7, 0.9]
BAND_EPSILON = 0.01   # K on either side of the bound: the single-phase states whose enthalpies bracket the band


def transition_bound() -> float:
    """The shared bound of AL2O3(a) and AL2O3(L): the upper end of the a record's last interval."""
    return RECORDS["AL2O3(a)"].intervals[-1].t_high


def plateau_rockets(writer: Writer) -> None:
    """One single-exit rocket case per pressure ratio: the pinned pair, its temperature, the plateau
    gamma_s and sound speed get fixtures the guard accepts."""
    reac, prod = make_mixtures(REACTANTS)
    descriptions = describe_reactants(REACTANTS, MASS_FRACTIONS, TEMPERATURES)
    for ratio in PRESSURE_RATIOS:
        solution, enthalpy = solve_rocket(reac, prod, MASS_FRACTIONS, TEMPERATURES, CHAMBER_PRESSURE_PA,
                                          FLOW_SHIFTING, False, pressure_ratios=[ratio])
        writer.case(
            "rocket", f"ap-htpb-al-plateau_pc7MPa_pip{ratio:g}",
            inputs=rocket_inputs(descriptions, prod.species_names, CHAMBER_PRESSURE_PA, enthalpy, FLOW_SHIFTING,
                                 False, pressure_ratios=[ratio]),
            outputs=rocket_outputs(solution, False, FLOW_SHIFTING), script_path=__file__)


def fuel_rich_hp(writer: Writer) -> None:
    """The include/remove cycle case: at O/F 0.50 the chamber's aluminium goes to carbide and nitride, the
    corner of the composition space where the equilibrium node's inclusion memory is exercised."""
    reac, prod = make_mixtures(REACTANTS)
    of_ratio = 0.5
    weights = reac.of_ratio_to_weights(np.array([1.0, 0.0, 0.0]), np.array([0.0, 14.0 / 32.0, 18.0 / 32.0]), of_ratio)
    descriptions = describe_reactants(REACTANTS, weights, TEMPERATURES)
    enthalpy = float(reac.calc_property(cea.ENTHALPY, weights, TEMPERATURES))
    pressure_pa = 7.0 * MPA_TO_PA
    outputs = solve_equilibrium(reac, prod, weights, "hp", enthalpy, pressure_pa, transport=False)
    writer.case(
        "hp", "ap-htpb-al-fuelrich_of0.5_pc7MPa",
        inputs=equilibrium_inputs(descriptions, prod.species_names, "hp", enthalpy, pressure_pa, False,
                                  of_ratio=of_ratio),
        outputs=outputs, script_path=__file__)


def band_hp(writer: Writer) -> None:
    """hp across the plateau: assigned enthalpies stepping through the AL2O3 latent-heat band at 700 kPa,
    interpolated between the single-phase tp states just under and just over the transition bound."""
    reac, prod = make_mixtures(REACTANTS)
    descriptions = describe_reactants(REACTANTS, MASS_FRACTIONS, TEMPERATURES)
    bound = transition_bound()
    below = solve_equilibrium(reac, prod, MASS_FRACTIONS, "tp", bound - BAND_EPSILON, BAND_PRESSURE_PA, False)
    above = solve_equilibrium(reac, prod, MASS_FRACTIONS, "tp", bound + BAND_EPSILON, BAND_PRESSURE_PA, False)
    for fraction in BAND_FRACTIONS:
        enthalpy = below["enthalpy"] + fraction * (above["enthalpy"] - below["enthalpy"])
        outputs = solve_equilibrium(reac, prod, MASS_FRACTIONS, "hp", enthalpy, BAND_PRESSURE_PA, False)
        writer.case(
            "hp", f"ap-htpb-al-plateau_p700kPa_band{fraction:g}",
            inputs=equilibrium_inputs(descriptions, prod.species_names, "hp", enthalpy, BAND_PRESSURE_PA, False,
                                      extra={"enthalpyAssigned": True, "bandFraction": fraction,
                                             "bandEnthalpies": [below["enthalpy"], above["enthalpy"]]}),
            outputs=outputs, script_path=__file__)


def bound_tp(writer: Writer) -> None:
    """tp exactly at the transition bound: the fixture pins which record the reference chooses there."""
    reac, prod = make_mixtures(REACTANTS)
    descriptions = describe_reactants(REACTANTS, MASS_FRACTIONS, TEMPERATURES)
    bound = transition_bound()
    outputs = solve_equilibrium(reac, prod, MASS_FRACTIONS, "tp", bound, BAND_PRESSURE_PA, transport=False)
    writer.case(
        "tp", f"ap-htpb-al-plateau_T{bound:g}",
        inputs=equilibrium_inputs(descriptions, prod.species_names, "tp", bound, BAND_PRESSURE_PA, False),
        outputs=outputs, script_path=__file__)


def generate(writer: Writer) -> None:
    plateau_rockets(writer)
    fuel_rich_hp(writer)
    band_hp(writer)
    bound_tp(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
