"""The throat family (2026-09-27): the chamber and the throat of a shifting-equilibrium rocket, with the throat
found as the largest mass flux rho*u over the package's own sp solves along the chamber isentrope, never taken
from the package's own rocket solver (Fixtures BOOT.md, the case matrix). It exists because the package's rocket
solver reports a wrong throat at the high-pressure edge of a melting plateau (Performance BOOT.md, "The throat
carries the largest mass flux"; RP-1311 sections 6.3.3 and 6.3.4).

Reuses the reactant lists and compositions of plateaus.py (AP/HTPB/Al) and rp1311.py (example 13's N2H4/Be/H2O2)
by import, never by copy. The scan solves only through cea_cases.solve_equilibrium, and the guard's rocket solves
only through cea_cases.solve_rocket.
"""
from __future__ import annotations

import math
import sys

import numpy as np

from cea_cases import FLOW_SHIFTING, describe_reactants, make_mixtures, rocket_inputs, solve_equilibrium, solve_rocket
from plateaus import MASS_FRACTIONS as AP_HTPB_AL_MASS_FRACTIONS
from plateaus import REACTANTS as AP_HTPB_AL_REACTANTS
from plateaus import TEMPERATURES as AP_HTPB_AL_TEMPERATURES
from rp1311 import (EXAMPLE13_FUEL, EXAMPLE13_INSERT, EXAMPLE13_OF_RATIO, EXAMPLE13_OXIDIZER, EXAMPLE13_REACTANTS,
                    EXAMPLE13_TEMPERATURES, EXAMPLE13_TRACE)
from rp1311 import example13_mixture
from writer import Writer, main_of

import cea

MPA_TO_PA = 1.0e6

# The chamber isentrope is scanned on 101 pressure ratios p/p_c from 0.45 to 0.70, refined by a 50-step ternary
# search on rho*u (Fixtures BOOT.md, the throat family's method).
GRID_LOW, GRID_HIGH, GRID_POINTS = 0.45, 0.70, 101
TERNARY_STEPS = 50

# The guard: where the package's own rocket throat is sonic within this Mach tolerance, the scan's c* must equal
# it within this relative tolerance (Fixtures BOOT.md, the throat family's guard).
GUARD_MACH_TOLERANCE = 1.0e-4
GUARD_CSTAR_RTOL = 1.0e-5

# (name, chamber pressure in Pa, enthalpy offset from the reactants' own h0 in J/kg) for the AP/HTPB/Al cases.
AP_HTPB_AL_CASES = [
    ("ap-htpb-al-throat_pc7MPa_dh-2.2MJkg", 7.0 * MPA_TO_PA, -2.20e6),
    ("ap-htpb-al-throat_pc7MPa_dh-2.225MJkg", 7.0 * MPA_TO_PA, -2.225e6),
    ("ap-htpb-al-throat_pc7MPa_dh-2.25MJkg", 7.0 * MPA_TO_PA, -2.25e6),
    ("ap-htpb-al-throat_pc7MPa_dh-2.275MJkg", 7.0 * MPA_TO_PA, -2.275e6),
    ("ap-htpb-al-throat_pc7MPa_dh-2.3MJkg", 7.0 * MPA_TO_PA, -2.30e6),
    ("ap-htpb-al-throat_pc1MPa_dh-2.25MJkg", 1.0 * MPA_TO_PA, -2.25e6),
    ("ap-htpb-al-throat_pc3MPa_dh-2.25MJkg", 3.0 * MPA_TO_PA, -2.25e6),
    ("ap-htpb-al-throat_pc15MPa_dh-2.25MJkg", 15.0 * MPA_TO_PA, -2.25e6),
]

EXAMPLE13_CHAMBER_PRESSURE_PA = 5.0 * MPA_TO_PA
EXAMPLE13_CASES = [
    ("rp1311-example13-throat_pc5MPa_dh0", 0.0),
    ("rp1311-example13-throat_pc5MPa_dh+250kJkg", 250.0e3),
]


def _flux(reac, prod, weights, entropy: float, chamber_pressure_pa: float, chamber_enthalpy: float, ratio: float,
         trace: float | None) -> tuple[float, dict, float]:
    """rho*u at one pressure ratio p/p_c: an sp solve at the chamber's entropy, u from the energy equation."""
    state = solve_equilibrium(reac, prod, weights, "sp", entropy, chamber_pressure_pa * ratio, False, trace=trace)
    velocity = math.sqrt(2.0 * (chamber_enthalpy - state["enthalpy"]))
    return state["density"] * velocity, state, velocity


def _scan_throat(reac, prod, weights, chamber_pressure_pa: float, enthalpy: float, trace: float | None = None):
    """The chamber's hp solve, then the largest rho*u over the grid, refined by ternary search; returns
    (chamber, throat, velocity, c*, p/p_c)."""
    chamber = solve_equilibrium(reac, prod, weights, "hp", enthalpy, chamber_pressure_pa, False, trace=trace)
    grid = np.linspace(GRID_LOW, GRID_HIGH, GRID_POINTS)
    fluxes = [_flux(reac, prod, weights, chamber["entropy"], chamber_pressure_pa, chamber["enthalpy"], r, trace)[0]
             for r in grid]
    peak = int(np.argmax(fluxes))
    if peak == 0 or peak == len(grid) - 1:
        raise RuntimeError(f"the largest mass flux lies at the grid edge (index {peak})")
    low, high = float(grid[peak - 1]), float(grid[peak + 1])
    for _ in range(TERNARY_STEPS):
        left, right = low + (high - low) / 3.0, high - (high - low) / 3.0
        if (_flux(reac, prod, weights, chamber["entropy"], chamber_pressure_pa, chamber["enthalpy"], left, trace)[0]
                < _flux(reac, prod, weights, chamber["entropy"], chamber_pressure_pa, chamber["enthalpy"], right, trace)[0]):
            low = left
        else:
            high = right
    mass_flux, throat, velocity = _flux(reac, prod, weights, chamber["entropy"], chamber_pressure_pa,
                                        chamber["enthalpy"], high, trace)
    return chamber, throat, velocity, chamber_pressure_pa / mass_flux, high


def _package_throat(reac, prod, weights, temperatures, chamber_pressure_pa: float, enthalpy: float,
                    insert: list[str] | None, trace: float | None) -> tuple[dict | None, str | None]:
    """The package's own rocket throat, for the record; None with the guard's error message when the package's
    own station guard rejects the solve, which it does exactly at the plateau edges this family exists for: found
    2026-09-27, `cea_cases.solve_rocket` raises before Mach or c* can be read, because its station guard runs right
    after the solve and this is exactly the chamber/throat entropy inconsistency it is built to catch."""
    try:
        solution, _ = solve_rocket(reac, prod, weights, temperatures, chamber_pressure_pa, FLOW_SHIFTING, False,
                                   enthalpy=enthalpy, insert=insert, trace=trace)
    except RuntimeError as guard_error:
        return None, str(guard_error)
    return {"cStar": float(solution.c_star[1]), "mach": float(solution.Mach[1]),
           "pressureRatio": float(solution.P[0] / solution.P[1])}, None


def _station(state: dict, label: str, index: int, mach: float, area_ratio: float, pressure_ratio: float,
            c_star: float, velocity: float) -> dict:
    """One station's output document: the equilibrium state fields, then the performance figures, moleFractions last."""
    mole_fractions = state["moleFractions"]
    body = {k: v for k, v in state.items() if k not in ("moleFractions", "converged")}
    station = {"station": label, "index": index, "frozen": False, **body}
    station.update({"mach": mach, "areaRatio": area_ratio, "pressureRatio": pressure_ratio,
                    "characteristicVelocity": c_star, "thrustCoefficient": velocity / c_star, "specificImpulse": velocity})
    station["moleFractions"] = mole_fractions
    return station


def _throat_outputs(name: str, reac, prod, weights, temperatures, chamber_pressure_pa: float, enthalpy: float,
                    insert: list[str] | None = None, trace: float | None = None) -> dict:
    """The scan's chamber and throat stations, checked against the package's rocket throat wherever it is sonic."""
    chamber, throat, velocity, c_star, ratio = _scan_throat(reac, prod, weights, chamber_pressure_pa, enthalpy, trace)
    mach = velocity / throat["soundSpeed"]
    outputs = {"stations": [
        _station(chamber, "chamber", 0, 0.0, 0.0, 1.0, c_star, 0.0),
        _station(throat, "throat", 1, mach, 1.0, chamber_pressure_pa / throat["pressure"], c_star, velocity),
    ]}
    package, guard_error = _package_throat(reac, prod, weights, temperatures, chamber_pressure_pa, enthalpy, insert, trace)
    if package is not None and abs(package["mach"] - 1.0) <= GUARD_MACH_TOLERANCE:
        if abs(package["cStar"] - c_star) > GUARD_CSTAR_RTOL * abs(package["cStar"]):
            raise RuntimeError(f"{name}: scan c* {c_star!r} against the package's sonic throat c* {package['cStar']!r}")
        print(f"    {name}: scan c* {c_star:.4f} matches the package's sonic throat {package['cStar']:.4f}")
    else:
        reason = guard_error if package is None else f"Mach {package['mach']:.4f}"
        print(f"    {name}: scan c* {c_star:.4f}, package rocket throat not usable ({reason})")
        outputs["packageRocketThroat"] = package if package is not None else {"guardError": guard_error}
    return outputs


def ap_htpb_al_throats(writer: Writer) -> None:
    reac, prod = make_mixtures(AP_HTPB_AL_REACTANTS)
    h0 = float(reac.calc_property(cea.ENTHALPY, AP_HTPB_AL_MASS_FRACTIONS, AP_HTPB_AL_TEMPERATURES))
    descriptions = describe_reactants(AP_HTPB_AL_REACTANTS, AP_HTPB_AL_MASS_FRACTIONS, AP_HTPB_AL_TEMPERATURES)
    for name, chamber_pressure_pa, dh in AP_HTPB_AL_CASES:
        enthalpy = h0 + dh
        outputs = _throat_outputs(name, reac, prod, AP_HTPB_AL_MASS_FRACTIONS, AP_HTPB_AL_TEMPERATURES,
                                  chamber_pressure_pa, enthalpy)
        writer.case(
            "throat", name,
            inputs=rocket_inputs(descriptions, prod.species_names, chamber_pressure_pa, enthalpy, FLOW_SHIFTING, False,
                                 extra={"enthalpyAssigned": True}),
            outputs=outputs, script_path=__file__, method="cea-package-mass-flux-scan")


def example13_throats(writer: Writer) -> None:
    reac, prod, weights = example13_mixture()
    h0 = float(reac.calc_property(cea.ENTHALPY, weights, EXAMPLE13_TEMPERATURES))
    descriptions = describe_reactants(EXAMPLE13_REACTANTS, weights, EXAMPLE13_TEMPERATURES,
                                      oxidizer=EXAMPLE13_OXIDIZER, fuel=EXAMPLE13_FUEL)
    for name, dh in EXAMPLE13_CASES:
        enthalpy = h0 + dh
        outputs = _throat_outputs(name, reac, prod, weights, EXAMPLE13_TEMPERATURES, EXAMPLE13_CHAMBER_PRESSURE_PA,
                                  enthalpy, insert=EXAMPLE13_INSERT, trace=EXAMPLE13_TRACE)
        writer.case(
            "throat", name,
            inputs=rocket_inputs(descriptions, prod.species_names, EXAMPLE13_CHAMBER_PRESSURE_PA, enthalpy,
                                 FLOW_SHIFTING, False, of_ratio=EXAMPLE13_OF_RATIO, trace=EXAMPLE13_TRACE,
                                 insert=EXAMPLE13_INSERT, extra={"enthalpyAssigned": True}),
            outputs=outputs, script_path=__file__, method="cea-package-mass-flux-scan")


def generate(writer: Writer) -> None:
    ap_htpb_al_throats(writer)
    example13_throats(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
