"""The four reference propellants of the case matrix: rocket cases over the O/F, pressure and flow grid,
and equilibrium cases derived from the stations of one central case per propellant; plus the sodium case,
one hp case of a fifth propellant pair added for its six-interval product (see below).

HTPB is not a thermo.inp record; the definition used here is provisional (see the Fixtures BOOT.md):
C 7.3165 H 10.3416 O 0.0674, assigned enthalpy -250 cal/mol at 298.15 K, molar mass from the
atomic weights of the file.
"""
from __future__ import annotations

import sys

import numpy as np

import cea

from cea_cases import (FLOW_FROZEN_CHAMBER, FLOW_FROZEN_THROAT, FLOW_SHIFTING, Custom, derive_equilibrium_cases,
                       describe_reactants, equilibrium_inputs, make_mixtures, rocket_inputs, rocket_outputs,
                       solve_equilibrium, solve_rocket)
from writer import Writer, main_of

MPA_TO_PA = 1.0e6

HTPB = Custom("HTPB", {"C": 7.3165, "H": 10.3416, "O": 0.0674}, -250.0,
              note="provisional definition, to be confirmed against a cited source (Fixtures BOOT.md)")

PROPELLANTS = [
    {
        "name": "lox-lh2",
        "reactants": ["O2(L)", "H2(L)"], "temperatures": [90.17, 20.27],
        "oxidizer": [1.0, 0.0], "fuel": [0.0, 1.0],
        "ofRatios": [4.0, 5.0, 6.0, 7.0, 8.0], "chamberPressuresMPa": [5.0, 7.0, 10.0],
        "areaRatios": [20.0, 77.5], "flows": [FLOW_SHIFTING, FLOW_FROZEN_CHAMBER, FLOW_FROZEN_THROAT],
        "derive": (6.0, 7.0),
    },
    {
        "name": "lox-rp1",
        "reactants": ["O2(L)", "RP-1"], "temperatures": [90.17, 298.15],
        "oxidizer": [1.0, 0.0], "fuel": [0.0, 1.0],
        "ofRatios": [2.0, 2.3, 2.6, 2.9, 3.2], "chamberPressuresMPa": [7.0, 10.0],
        "areaRatios": [16.0, 40.0], "flows": [FLOW_SHIFTING, FLOW_FROZEN_THROAT],
        "derive": (2.6, 10.0),
    },
    {
        "name": "nto-udmh",
        "reactants": ["N2O4(L)", "C2H8N2(L),UDMH"], "temperatures": [298.15, 298.15],
        "oxidizer": [1.0, 0.0], "fuel": [0.0, 1.0],
        "ofRatios": [1.8, 2.0, 2.2, 2.4, 2.6], "chamberPressuresMPa": [1.0, 2.0],
        "areaRatios": [10.0, 50.0], "flows": [FLOW_SHIFTING, FLOW_FROZEN_THROAT],
        "derive": (2.2, 2.0),
    },
    {
        "name": "ap-htpb-al",
        "reactants": ["NH4CLO4(I)", HTPB, "AL(cr)"], "temperatures": [298.15, 298.15, 298.15],
        "massFractions": [0.68, 0.14, 0.18],
        "chamberPressuresMPa": [5.0, 7.0],
        "areaRatios": [8.0, 12.0], "flows": [FLOW_SHIFTING],
        "derive": (None, 7.0),
    },
]


def case_name(prop: dict, of_ratio, pc_mpa: float, flow: str) -> str:
    of_part = "" if of_ratio is None else f"_of{of_ratio:g}"
    return f"{prop['name']}{of_part}_pc{pc_mpa:g}MPa_{flow}"


def generate_propellant(writer: Writer, prop: dict) -> None:
    reac, prod = make_mixtures(prop["reactants"])
    temperatures = np.array(prop["temperatures"])
    of_ratios = prop.get("ofRatios", [None])
    for of_ratio in of_ratios:
        if of_ratio is None:
            weights = np.array(prop["massFractions"])
            descriptions = describe_reactants(prop["reactants"], weights, temperatures)
        else:
            oxidizer, fuel = np.array(prop["oxidizer"]), np.array(prop["fuel"])
            weights = reac.of_ratio_to_weights(oxidizer, fuel, of_ratio)
            descriptions = describe_reactants(prop["reactants"], weights, temperatures, oxidizer=oxidizer, fuel=fuel)
        for pc_mpa in prop["chamberPressuresMPa"]:
            chamber_pressure_pa = pc_mpa * MPA_TO_PA
            for flow in prop["flows"]:
                # The package's frozen expansion does not converge with transport on for product sets of
                # about a hundred species and more (LOX/RP-1, NTO/UDMH); frozen cases carry no transport.
                transport = flow == FLOW_SHIFTING
                solution, enthalpy = solve_rocket(reac, prod, weights, temperatures, chamber_pressure_pa, flow, transport,
                                                  area_ratios=prop["areaRatios"])
                name = case_name(prop, of_ratio, pc_mpa, flow)
                writer.case(
                    "rocket", name,
                    inputs=rocket_inputs(descriptions, prod.species_names, chamber_pressure_pa, enthalpy, flow, transport,
                                         area_ratios=prop["areaRatios"], of_ratio=of_ratio),
                    outputs=rocket_outputs(solution, transport, flow), script_path=__file__)
                if flow == FLOW_SHIFTING and (of_ratio, pc_mpa) == prop["derive"]:
                    # Derived cases carry no transport: with transport on, the package reports the gas-phase
                    # frozen heat capacities for a mixture with condensed species (Fixtures BOOT.md).
                    derive_equilibrium_cases(writer, __file__, name, solution, reac, prod, weights, descriptions, False,
                                             list(range(solution.num_pts)), of_ratio=of_ratio)


SODIUM_REACTANTS = ["NaNO3(a)", "RP-1"]
SODIUM_TEMPERATURES = np.array([298.15, 298.15])
SODIUM_OXIDIZER = np.array([1.0, 0.0])
SODIUM_FUEL = np.array([0.0, 1.0])
SODIUM_OF_RATIO = 4.0
SODIUM_PRESSURE_PA = 7.0 * MPA_TO_PA


def sodium_hp(writer: Writer) -> None:
    """The sodium case (2026-09-27, the Fixtures case matrix): NaNO3(a) with RP-1, both at 298.15 K, O/F 4,
    7 MPa, one hp case. Its candidate products include NaCN(II), the six-interval record the Thermo node's
    table limit refused until that day (its BOOT.md)."""
    reac, prod = make_mixtures(SODIUM_REACTANTS)
    weights = reac.of_ratio_to_weights(SODIUM_OXIDIZER, SODIUM_FUEL, SODIUM_OF_RATIO)
    descriptions = describe_reactants(SODIUM_REACTANTS, weights, SODIUM_TEMPERATURES,
                                      oxidizer=SODIUM_OXIDIZER, fuel=SODIUM_FUEL)
    enthalpy = float(reac.calc_property(cea.ENTHALPY, weights, SODIUM_TEMPERATURES))
    outputs = solve_equilibrium(reac, prod, weights, "hp", enthalpy, SODIUM_PRESSURE_PA, transport=False)
    writer.case(
        "hp", "nano3-rp1_of4_pc7MPa",
        inputs=equilibrium_inputs(descriptions, prod.species_names, "hp", enthalpy, SODIUM_PRESSURE_PA, False,
                                  of_ratio=SODIUM_OF_RATIO),
        outputs=outputs, script_path=__file__)


BR2_ANOMALY_NAME = "Br2(cr)"
BR2_ANOMALY_TEMPERATURE = 298.15


def br2_reactant_anomaly(writer: Writer) -> None:
    """Br2(cr) as a reactant at 298.15 K (2026-09-28, the second audit's observation 6): its one interval is
    written 300 -> 265.9 K, an inverted range that used to leave the reactant unusable at any temperature
    (Problems BOOT.md, the second audit's fixes). It cannot be recorded as an "hp" or "rocket" case: the package
    itself does not converge a pure-Br2(cr) equilibrium at any enthalpy near this one ("Mixture temperature
    outside of allowable bounds", `EqSolver.solve: CEA_NOT_CONVERGED`, checked by hand against the pinned
    package), because the record is equally out of range for the equilibrium routine's own candidacy test
    (Thermo BOOT.md, 2026-09-26: "Br2(cr) is in range nowhere, in the reference too"). This is a reactant-level
    fact instead, the package's own per-kilogram enthalpy of the reactant alone at its temperature
    (`Mixture.calc_property`, the same call every other fixture's `reactantEnthalpy` comes from), with no
    equilibrium solve and so a new, lightweight case kind ("reactant") rather than "hp"."""
    reac, _ = make_mixtures([BR2_ANOMALY_NAME])
    weights = np.array([1.0])
    temperatures = np.array([BR2_ANOMALY_TEMPERATURE])
    enthalpy = float(reac.calc_property(cea.ENTHALPY, weights, temperatures))
    descriptions = describe_reactants([BR2_ANOMALY_NAME], weights, temperatures)
    writer.case(
        "reactant", f"{BR2_ANOMALY_NAME}_{BR2_ANOMALY_TEMPERATURE:g}K",
        inputs={"reactants": descriptions, "temperature": BR2_ANOMALY_TEMPERATURE},
        outputs={"enthalpyPerKilogram": enthalpy},
        script_path=__file__)


def generate(writer: Writer) -> None:
    for prop in PROPELLANTS:
        generate_propellant(writer, prop)
    sodium_hp(writer)
    br2_reactant_anomaly(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
