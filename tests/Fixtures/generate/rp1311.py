"""The seven RP-1311 examples of the case matrix, with the inputs of the package's own sample scripts.

Examples 1 and 14 are tp problems, 3 and 5 hp problems, 8, 12 and 13 rocket problems (12
frozen from the throat; 13 the beryllium rocket whose throat and first exit sit on the BeO
melting plateau, solved with the package's `insert` list). Equilibrium cases are derived from
the stations of examples 8 and 12 (chamber and throat only for 12, whose exits are frozen),
and tp cases at the BeO transition bounds from example 13's mixture.
"""
from __future__ import annotations

import sys

import numpy as np

import cea

from cea_cases import (FLOW_FROZEN_THROAT, FLOW_SHIFTING, RECORDS, Custom, derive_equilibrium_cases,
                       describe_reactants, equilibrium_inputs, make_mixtures, rocket_inputs, rocket_outputs,
                       solve_equilibrium, solve_rocket)
from common import BAR_TO_PA
from writer import Writer, main_of

EXAMPLE1_PRODUCTS = ["Ar", "C", "CO", "CO2", "H", "H2", "H2O", "HNO", "HO2", "HNO2",
                     "HNO3", "N", "NH", "NO", "N2", "N2O3", "O", "O2", "OH", "O3"]

EXAMPLE3_OMIT = [
    "CCN", "CNC", "C2N2", "C2O",
    "C3H4,allene", "C3H4,propyne", "C3H4,cyclo-", "C3",
    "C3H5,allyl", "C3H6,propylene", "C3H6,cyclo-", "C3H3,propargyl",
    "C3H6O", "C3H7,n-propyl", "C3H7,i-propyl", "Jet-A(g)",
    "C3O2", "C4", "C4H2", "C3H8O,2propanol",
    "C4H4,1,3-cyclo-", "C4H6,butadiene", "C4H6,2-butyne", "C3H8O,1propanol",
    "C4H8,tr2-butene", "C4H8,isobutene", "C4H8,cyclo-", "C4H6,cyclo-",
    "(CH3COOH)2", "C4H9,n-butyl", "C4H9,i-butyl", "C4H8,1-butene",
    "C4H9,s-butyl", "C4H9,t-butyl", "C4H10,isobutane", "C4H8,cis2-buten",
    "C4H10,n-butane", "C4N2", "C5", "C3H8",
    "C5H6,1,3cyclo-", "C5H8,cyclo-", "C5H10,1-pentene", "C10H21,n-decyl",
    "C5H10,cyclo-", "C5H11,pentyl", "C5H11,t-pentyl", "C12H10,biphenyl",
    "C5H12,n-pentane", "C5H12,i-pentane", "CH3C(CH3)2CH3", "C12H9,o-bipheny",
    "C6H6", "C6H5OH,phenol", "C6H10,cyclo-", "C6H2",
    "C6H12,1-hexene", "C6H12,cyclo-", "C6H13,n-hexyl", "C6H5,phenyl",
    "C7H7,benzyl", "C7H8", "C7H8O,cresol-mx", "C6H5O,phenoxy",
    "C7H14,1-heptene", "C7H15,n-heptyl", "C7H16,n-heptane", "C10H8,azulene",
    "C8H8,styrene", "C8H10,ethylbenz", "C8H16,1-octene", "C10H8,napthlene",
    "C8H17,n-octyl", "C8H18,isooctane", "C8H18,n-octane", "C9H19,n-nonyl",
    "Jet-A(L)", "C6H6(L)", "H2O(s)", "H2O(L)",
]

EXAMPLE5_OMIT = [
    "COOH", "C2", "C2H", "CHCO,ketyl", "C2H2,vinylidene", "CH2CO,ketene", "C2H3,vinyl",
    "CH3CO,acetyl", "C2H4O,ethylen-o", "CH3CHO,ethanal", "CH3COOH", "(HCOOH)2",
    "C2H5", "C2H6", "CH3N2CH3", "CH3OCH3", "C2H5OH", "CCN", "CNC", "C2N2",
    "C2O", "C3", "C3H3,propargyl", "C3H4,allene", "C3H4,propyne", "C3H4,cyclo-",
    "C3H5,allyl", "C3H6,propylene", "C3H6,cyclo-", "C3H6O", "C3H7,n-propyl",
    "C3H7,i-propyl", "C3H8", "C3H8O,1propanol", "C3H8O,2propanol", "C3O2",
    "C4", "C4H2", "C4H4,1,3-cyclo-", "C4H6,butadiene", "C4H6,2-butyne", "C4H6,cyclo-",
    "C4H8,1-butene", "C4H8,cis2-buten", "C4H8,tr2-butene", "C4H8,isobutene", "C4H8,cyclo-",
    "(CH3COOH)2", "C4H9,n-butyl", "C4H9,i-butyl", "C4H9,s-butyl", "C4H9,t-butyl",
    "C4H10,isobutane", "C4H10,n-butane", "C4N2", "C5", "C5H6,1,3cyclo-", "C5H8,cyclo-",
    "C5H10,1-pentene", "C5H10,cyclo-", "C5H11,pentyl", "C5H11,t-pentyl", "C5H12,n-pentane",
    "C5H12,i-pentane", "CH3C(CH3)2CH3", "C6H2", "C6H5,phenyl", "C6H5O,phenoxy",
    "C6H6", "C6H5OH,phenol", "C6H10,cyclo-", "C6H12,1-hexene", "C6H12,cyclo-",
    "C6H13,n-hexyl", "C7H7,benzyl", "C7H8", "C7H8O,cresol-mx", "C7H14,1-heptene",
    "C7H15,n-heptyl", "C7H16,n-heptane", "C8H8,styrene", "C8H10,ethylbenz",
    "C8H16,1-octene", "C8H17,n-octyl", "C8H18,isooctane", "C8H18,n-octane",
    "C9H19,n-nonyl", "C10H8,naphthale", "C10H21,n-decyl", "C12H9,o-bipheny", "C12H10,biphenyl",
    "Jet-A(g)", "HNCO", "HNO", "HNO2", "HNO3", "HCCN", "HCHO,formaldehy", "HCOOH",
    "NH", "NH2", "NH2OH", "NCN", "N2H2", "NH2NO2", "N2H4", "H2O2",
    "(HCOOH)2", "C6H6(L)", "C7H8(L)", "C8H18(L),n-octa", "Jet-A(L)", "H2O(s)", "H2O(L)",
]

EXAMPLE12_PRODUCTS = ["CO", "CO2", "H", "HNO", "HNO2", "HO2",
                      "H2", "H2O", "H2O2", "N", "NO", "NO2",
                      "N2", "N2O", "O", "OH", "O2", "HCO", "NH",
                      "CH4", "NH2", "NH3", "H2O(L)", "C(gr)"]


def example1(writer: Writer) -> None:
    reactants = ["H2", "Air"]
    reac, prod = make_mixtures(reactants, products=EXAMPLE1_PRODUCTS)
    fuel_weights = reac.moles_to_weights(np.array([1.0, 0.0]))
    oxidant_weights = reac.moles_to_weights(np.array([0.0, 1.0]))
    for eq_ratio in [1.0, 1.5]:
        of_ratio = reac.chem_eq_ratio_to_of_ratio(oxidant_weights, fuel_weights, eq_ratio)
        weights = reac.of_ratio_to_weights(oxidant_weights, fuel_weights, of_ratio)
        descriptions = describe_reactants(reactants, weights)
        for p_atm in [1.0, 0.1, 0.01]:
            pressure_pa = cea.units.atm_to_bar(p_atm) * BAR_TO_PA
            for t in [3000.0, 2000.0]:
                outputs = solve_equilibrium(reac, prod, weights, "tp", t, pressure_pa, transport=False)
                writer.case(
                    "tp", f"rp1311-example1_r{eq_ratio}_p{p_atm}atm_T{t:g}",
                    inputs=equilibrium_inputs(descriptions, prod.species_names, "tp", t, pressure_pa, False,
                                              of_ratio=float(of_ratio), extra={"chemicalEquivalenceRatio": eq_ratio},
                                              only=EXAMPLE1_PRODUCTS),
                    outputs=outputs, script_path=__file__)


def example3(writer: Writer) -> None:
    reactants = ["Air", "C7H8(L)", "C8H18(L),n-octa"]
    temperatures = np.array([700.0, 298.15, 298.15])
    reac, prod = make_mixtures(reactants, omit=EXAMPLE3_OMIT)
    weights = reac.of_ratio_to_weights(np.array([1.0, 0.0, 0.0]), np.array([0.0, 0.4, 0.6]), 17.0)
    descriptions = describe_reactants(reactants, weights, temperatures)
    enthalpy = float(reac.calc_property(cea.ENTHALPY, weights, temperatures))
    for p_bar in [100.0, 10.0, 1.0]:
        pressure_pa = p_bar * BAR_TO_PA
        outputs = solve_equilibrium(reac, prod, weights, "hp", enthalpy, pressure_pa, transport=False, trace=1e-15)
        writer.case(
            "hp", f"rp1311-example3_p{p_bar:g}bar",
            inputs=equilibrium_inputs(descriptions, prod.species_names, "hp", enthalpy, pressure_pa, False,
                                      of_ratio=17.0, omit=EXAMPLE3_OMIT, trace=1e-15),
            outputs=outputs, script_path=__file__)


def example5(writer: Writer) -> None:
    binder = Custom("CHOS-Binder", {"C": 1.0, "H": 1.86955, "O": 0.031256, "S": 0.008415}, -2999.082,
                    note="RP-1311 example 5 custom reactant")
    reactants = ["NH4CLO4(I)", binder, "AL(cr)", "MgO(cr)", "H2O(L)"]
    weights = np.array([0.7206, 0.1858, 0.09, 0.002, 0.0016])
    reac, prod = make_mixtures(reactants, omit=EXAMPLE5_OMIT)
    descriptions = describe_reactants(reactants, weights, 298.15)
    enthalpy = float(reac.calc_property(cea.ENTHALPY, weights, np.full(5, 298.15)))
    for p_bar in [34.473652, 17.236826, 8.618413, 3.447365, 0.344737]:
        pressure_pa = p_bar * BAR_TO_PA
        outputs = solve_equilibrium(reac, prod, weights, "hp", enthalpy, pressure_pa, transport=False)
        writer.case(
            "hp", f"rp1311-example5_p{p_bar:g}bar",
            inputs=equilibrium_inputs(descriptions, prod.species_names, "hp", enthalpy, pressure_pa, False,
                                      omit=EXAMPLE5_OMIT),
            outputs=outputs, script_path=__file__)


def example8(writer: Writer) -> None:
    reactants = ["H2(L)", "O2(L)"]
    temperatures = np.array([20.27, 90.17])
    of_ratio = 5.55157
    reac, prod = make_mixtures(reactants)
    weights = reac.of_ratio_to_weights(np.array([0.0, 1.0]), np.array([1.0, 0.0]), of_ratio)
    descriptions = describe_reactants(reactants, weights, temperatures)
    chamber_pressure_pa = 53.3172 * BAR_TO_PA
    pressure_ratios, subsonic, area_ratios = [10.0, 100.0, 1000.0], [1.58], [25.0, 50.0, 75.0]
    solution, enthalpy = solve_rocket(reac, prod, weights, temperatures, chamber_pressure_pa, FLOW_SHIFTING, True,
                                      area_ratios=area_ratios, pressure_ratios=pressure_ratios,
                                      subsonic_area_ratios=subsonic)
    name = "rp1311-example8"
    writer.case(
        "rocket", name,
        inputs=rocket_inputs(descriptions, prod.species_names, chamber_pressure_pa, enthalpy, FLOW_SHIFTING, True,
                             area_ratios=area_ratios, pressure_ratios=pressure_ratios, subsonic_area_ratios=subsonic,
                             of_ratio=of_ratio),
        outputs=rocket_outputs(solution, True, FLOW_SHIFTING), script_path=__file__)
    supersonic = [i for i in range(solution.num_pts) if i < 2 or solution.Mach[i] > 1.0]
    derive_equilibrium_cases(writer, __file__, name, solution, reac, prod, weights, descriptions, False, supersonic,
                             of_ratio=of_ratio)


def example12(writer: Writer) -> None:
    reactants = ["CH6N2(L)", "N2O4(L)"]
    temperatures = np.array([298.15, 298.15])
    of_ratio = 2.5
    reac, prod = make_mixtures(reactants, products=EXAMPLE12_PRODUCTS)
    weights = reac.of_ratio_to_weights(np.array([0.0, 1.0]), np.array([1.0, 0.0]), of_ratio)
    descriptions = describe_reactants(reactants, weights, temperatures)
    chamber_pressure_pa = cea.units.psi_to_bar(1000.0) * BAR_TO_PA
    pressure_ratios, area_ratios = [68.0457], [5.0, 10.0, 25.0, 50.0, 75.0, 100.0, 150.0, 200.0]
    solution, enthalpy = solve_rocket(reac, prod, weights, temperatures, chamber_pressure_pa, FLOW_FROZEN_THROAT, True,
                                      area_ratios=area_ratios, pressure_ratios=pressure_ratios)
    name = "rp1311-example12"
    writer.case(
        "rocket", name,
        inputs=rocket_inputs(descriptions, prod.species_names, chamber_pressure_pa, enthalpy, FLOW_FROZEN_THROAT, True,
                             area_ratios=area_ratios, pressure_ratios=pressure_ratios, of_ratio=of_ratio,
                             only=EXAMPLE12_PRODUCTS),
        outputs=rocket_outputs(solution, True, FLOW_FROZEN_THROAT), script_path=__file__)
    derive_equilibrium_cases(writer, __file__, name, solution, reac, prod, weights, descriptions, False, [0, 1],
                             of_ratio=of_ratio, only=EXAMPLE12_PRODUCTS)


def example13(writer: Writer) -> None:
    """The beryllium rocket: N2H4/Be 80/20 fuel with H2O2 at O/F 33/67, 3000 psia, exits p_c/p 3, 10, 30, 300.

    The throat and the first exit sit on the BeO(b)/BeO(L) melting plateau at 2851 K, which the package only
    converges with `insert` seeding the liquid; without it the case loses 0.61 % of Ivac (Fixtures BOOT.md,
    the station guard). The trace threshold keeps the beryllium condensed pair printed at every station."""
    reactants = ["N2H4(L)", "Be(a)", "H2O2(L)"]
    temperatures = np.array([298.15, 298.15, 298.15])
    of_ratio = 33.0 / 67.0
    reac, prod = make_mixtures(reactants)
    weights = reac.of_ratio_to_weights(np.array([0.0, 0.0, 1.0]), np.array([0.8, 0.2, 0.0]), of_ratio)
    descriptions = describe_reactants(reactants, weights, temperatures)
    chamber_pressure_pa = cea.units.psi_to_bar(3000.0) * BAR_TO_PA
    pressure_ratios = [3.0, 10.0, 30.0, 300.0]
    insert = ["BeO(L)"]
    trace = 1e-10
    solution, enthalpy = solve_rocket(reac, prod, weights, temperatures, chamber_pressure_pa, FLOW_SHIFTING, False,
                                      pressure_ratios=pressure_ratios, insert=insert, trace=trace)
    writer.case(
        "rocket", "rp1311-example13",
        inputs=rocket_inputs(descriptions, prod.species_names, chamber_pressure_pa, enthalpy, FLOW_SHIFTING, False,
                             pressure_ratios=pressure_ratios, of_ratio=of_ratio, trace=trace, insert=insert),
        outputs=rocket_outputs(solution, False, FLOW_SHIFTING), script_path=__file__)

    # tp exactly at the BeO transition bounds (the case matrix): the record chosen at a bound has a fixture.
    beo_b = RECORDS["BeO(b)"]
    for t in [beo_b.intervals[-1].t_high, beo_b.intervals[0].t_low]:
        pressure_pa = 10.0e6
        outputs = solve_equilibrium(reac, prod, weights, "tp", float(t), pressure_pa, transport=False)
        writer.case(
            "tp", f"rp1311-example13-mixture_T{t:g}",
            inputs=equilibrium_inputs(descriptions, prod.species_names, "tp", float(t), pressure_pa, False,
                                      of_ratio=of_ratio),
            outputs=outputs, script_path=__file__)


def example14(writer: Writer) -> None:
    reactants = ["H2(L)", "O2(L)"]
    reac, prod = make_mixtures(reactants)
    weights = reac.moles_to_weights(np.array([100.0, 0.0])) + reac.moles_to_weights(np.array([0.0, 60.0]))
    descriptions = describe_reactants(reactants, weights)
    pressure_pa = cea.units.atm_to_bar(0.05) * BAR_TO_PA
    for t in [1000.0, 500.0, 350.0, 305.0, 304.3, 304.2, 304.0, 300.0]:
        outputs = solve_equilibrium(reac, prod, weights, "tp", t, pressure_pa, transport=False)
        writer.case(
            "tp", f"rp1311-example14_T{t:g}",
            inputs=equilibrium_inputs(descriptions, prod.species_names, "tp", t, pressure_pa, False,
                                      extra={"fuelMoles": 100.0, "oxidantMoles": 60.0}),
            outputs=outputs, script_path=__file__)


def generate(writer: Writer) -> None:
    example1(writer)
    example3(writer)
    example5(writer)
    example8(writer)
    example12(writer)
    example13(writer)
    example14(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
