"""The throat family (2026-09-27): the chamber and the throat of a shifting-equilibrium rocket, with the throat
found as the first local maximum of the mass flux rho*u met from the chamber, over the package's own sp solves
along the chamber isentrope, never taken from the package's own rocket solver (Fixtures BOOT.md, the case
matrix). It exists because the package's rocket solver reports a wrong throat at the high-pressure edge of a
melting plateau, and (the second hidden-defect audit's finding F1, 2026-09-28) can also converge to a second,
larger-rho*u maximum further downstream of the true, first one near such a plateau (Performance BOOT.md, "the
throat is the first maximum of the mass flux met from the chamber"; RP-1311 sections 6.3.3 and 6.3.4).

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
    # The second hidden-defect audit's finding F1 (2026-09-28): the true (first, upstream) maximum sits on the
    # pinned AL2O3(a)/AL2O3(L) pair itself, with a second, larger-rho*u maximum further downstream that the tree
    # used to return (Performance BOOT.md, "the throat is the first maximum of the mass flux met from the chamber").
    ("ap-htpb-al-throat_pc7MPa_dh-2.625MJkg", 7.0 * MPA_TO_PA, -2.625e6),
]

EXAMPLE13_CHAMBER_PRESSURE_PA = 5.0 * MPA_TO_PA
EXAMPLE13_CASES = [
    ("rp1311-example13-throat_pc5MPa_dh0", 0.0),
    ("rp1311-example13-throat_pc5MPa_dh+250kJkg", 250.0e3),
]

# The second hidden-defect audit's finding F1 (2026-09-28), the two further cases outside the AP/HTPB/Al and
# example 13 systems: plain elemental reactants at 298.15 K, whose reference-state enthalpy is zero, so the
# enthalpy below is assigned directly rather than as an offset from a propellant's own h0. Reproduces, by element
# and mass fraction, the audit's own "b-o-h" and "li-f-h" element systems
# (scratchpad/audit2/harness/pt/performance/ZzAuditThroatSweep.cs) and its "al-o-h-lean" system, at the pressures
# and enthalpies its own sweep found the second, larger-rho*u maximum at (scratchpad/audit2/out/pt/throat-sweep-misc.csv).
# (name, reactants, mass fractions, chamber pressure in Pa, enthalpy in J/kg)
ELEMENT_MIXTURE_CASES = [
    ("al-o-h-lean-throat_pc7MPa", ["AL(cr)", "O2", "H2"], [0.08, 0.62, 0.30], 7.0 * MPA_TO_PA, 1.9125e6),
    ("b2o3-throat_pc0.3MPa", ["B(b)", "O2", "H2"], [0.10, 0.55, 0.35], 0.3 * MPA_TO_PA, -7.775e6),
    ("lif-throat_pc7MPa", ["Li(cr)", "F2", "H2"], [0.08, 0.62, 0.30], 7.0 * MPA_TO_PA, -7.575e6),
]
ELEMENT_MIXTURE_TEMPERATURE_K = 298.15

# The second hidden-defect audit's finding F3 (2026-09-28): the plateau-edge re-solve can land on the far side of
# the edge (Mach above 1) when it is not checked against the bracket's own subsonic fingerprint and u^2/a^2 < 1
# (Performance BOOT.md). Representative points of the audit's own Li2O (li-o-h) and BeO/H2O (be-o-h) systems
# (scratchpad/audit2/harness/pt/performance/ZzAuditThroatSweep.cs, its "li-o-h" and "be-o-h" systems), at the two
# edges of the 0.3 MPa Li2O band (14 cases in the audit's own sweep, h 3.29375 to 3.375 MJ/kg), both of the 3 MPa
# Li2O band (2 cases, h 2.20625 to 2.2125 MJ/kg) and the single BeO/H2O point at 15 MPa the audit found Mach 1.011
# at (scratchpad/audit2/out/pt/throat-sweep-misc.csv and throat-sweep-beMgZr.csv, "fine" rows with mach > 1). The
# full bands are covered without a fixture per point by the fact's own sweep
# (Performance.Tests/SecondAuditFixTests.cs, `ThePlateauEdgeIsSinglePhaseAndSubsonicOverTheAuditsLi2OAndBeOBands`).
# (name, reactants, mass fractions, chamber pressure in Pa, enthalpy in J/kg)
PLATEAU_EDGE_CASES = [
    ("li2o-throat_pc0.3MPa_h3.29375MJkg", ["Li(cr)", "O2", "H2"], [0.10, 0.50, 0.40], 0.3 * MPA_TO_PA, 3.29375e6),
    ("li2o-throat_pc3MPa_h2.2375MJkg", ["Li(cr)", "O2", "H2"], [0.10, 0.50, 0.40], 3.0 * MPA_TO_PA, 2.2375e6),
    ("beo-h2o-throat_pc15MPa_h-11.06875MJkg", ["Be(a)", "O2", "H2"], [0.10, 0.55, 0.35], 15.0 * MPA_TO_PA, -11.06875e6),
]

# The third audit pass of 2026-09-28 (part 2, finding 1): the plateau-edge acceptance's eight linear steps of the
# bracket width (finding F3's own fix, above) never reached the single-phase side of the 3 MPa Li2O band's own
# edge, turning 10 of the band's 12 cases into `ThroatNotFound` (Performance BOOT.md, the Constraints section's
# throat bullet). One fixture inside that band, at h 2.20625 MJ/kg (one of the audit's own two representative
# points cited in the `PLATEAU_EDGE_CASES` comment above), so the fixture-level throat-family test
# (`Performance.Tests/ThroatFixtureTests`) covers the same band the wider sweep facts
# (`Performance.Tests/ThirdPassFixTests.TheLi2OBandAt0Point3MPaNeverEndsThroatNotFound` and
# `.TheLi2OBandAt3MPaNeverEndsThroatNotFound`) walk.
# (name, reactants, mass fractions, chamber pressure in Pa, enthalpy in J/kg)
THIRD_PASS_REACH_CASES = [
    ("li2o-throat_pc3MPa_h2.20625MJkg", ["Li(cr)", "O2", "H2"], [0.10, 0.50, 0.40], 3.0 * MPA_TO_PA, 2.20625e6),
]


def _flux(reac, prod, weights, entropy: float, chamber_pressure_pa: float, chamber_enthalpy: float, ratio: float,
         trace: float | None) -> tuple[float, dict, float]:
    """rho*u at one pressure ratio p/p_c: an sp solve at the chamber's entropy, u from the energy equation."""
    state = solve_equilibrium(reac, prod, weights, "sp", entropy, chamber_pressure_pa * ratio, False, trace=trace)
    velocity = math.sqrt(2.0 * (chamber_enthalpy - state["enthalpy"]))
    return state["density"] * velocity, state, velocity


def _flux_or_negative_infinity(reac, prod, weights, entropy: float, chamber_pressure_pa: float, chamber_enthalpy: float,
                               ratio: float, trace: float | None) -> float:
    """`_flux`'s value, or negative infinity when the sp solve does not converge (the second hidden-defect audit's
    finding F3, 2026-09-28: a ternary-search trial can land arbitrarily close to a melting plateau's own edge,
    where even the reference package's equilibrium solver is not guaranteed to converge). A non-convergent trial is
    worse than any converged one, so the search always moves away from it, never toward it, and the razor-thin
    non-converging point found at the Li2O plateau edge (`tests/Fixtures/BOOT.md`, the throat family's entry) is
    stepped past rather than raised."""
    try:
        return _flux(reac, prod, weights, entropy, chamber_pressure_pa, chamber_enthalpy, ratio, trace)[0]
    except RuntimeError:
        return -math.inf


def _first_local_max(fluxes: list[float]) -> int:
    """The first local maximum of rho*u scanning from the chamber side (the grid's high-ratio, high-pressure end)
    toward the low-pressure end (Performance BOOT.md, 2026-09-28, "the throat is the first maximum of the mass flux
    met from the chamber"): the highest-pressure index where the flux stops rising. Near a melting plateau there can
    be a second, lower-pressure maximum further down the grid; this picks the one a converging nozzle's subsonic
    flow actually reaches first. Where none is found scanning this way, the grid's own edge (`len(fluxes) - 1`) is
    returned, so the caller's existing edge check raises the same way it always did."""
    for i in range(len(fluxes) - 2, 0, -1):
        if fluxes[i] > fluxes[i - 1] and fluxes[i] >= fluxes[i + 1]:
            return i
    return len(fluxes) - 1


def _scan_throat(reac, prod, weights, chamber_pressure_pa: float, enthalpy: float, trace: float | None = None):
    """The chamber's hp solve, then the first rho*u maximum met from the chamber over the grid, refined by ternary
    search; returns (chamber, throat, velocity, c*, p/p_c)."""
    chamber = solve_equilibrium(reac, prod, weights, "hp", enthalpy, chamber_pressure_pa, False, trace=trace)
    grid = np.linspace(GRID_LOW, GRID_HIGH, GRID_POINTS)
    fluxes = [_flux(reac, prod, weights, chamber["entropy"], chamber_pressure_pa, chamber["enthalpy"], r, trace)[0]
             for r in grid]
    peak = _first_local_max(fluxes)
    if peak == 0 or peak == len(grid) - 1:
        raise RuntimeError(f"the first local maximum lies at the grid edge (index {peak})")
    low, high = float(grid[peak - 1]), float(grid[peak + 1])
    for _ in range(TERNARY_STEPS):
        left, right = low + (high - low) / 3.0, high - (high - low) / 3.0
        flux_left = _flux_or_negative_infinity(reac, prod, weights, chamber["entropy"], chamber_pressure_pa,
                                               chamber["enthalpy"], left, trace)
        flux_right = _flux_or_negative_infinity(reac, prod, weights, chamber["entropy"], chamber_pressure_pa,
                                                chamber["enthalpy"], right, trace)
        if flux_left < flux_right:
            low = left
        else:
            high = right

    # The converged bracket's own edge, `high`, is a deterministic function of the coarse grid and TERNARY_STEPS
    # (never one of the razor-thin non-convergent ratios the loop above stepped past, which is why it is not
    # itself guarded); a tiny nudge toward the bracket's midpoint is the one retry needed on the rare chance it
    # still lands on one (finding F3's evidence-gathering: none of the family's fixtures has needed it).
    try:
        mass_flux, throat, velocity = _flux(reac, prod, weights, chamber["entropy"], chamber_pressure_pa,
                                            chamber["enthalpy"], high, trace)
    except RuntimeError:
        high = 0.5 * (low + high)
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
    if package is None or abs(package["mach"] - 1.0) > GUARD_MACH_TOLERANCE:
        reason = guard_error if package is None else f"Mach {package['mach']:.4f}"
        print(f"    {name}: scan c* {c_star:.4f}, package rocket throat not usable ({reason})")
        outputs["packageRocketThroat"] = package if package is not None else {"guardError": guard_error}
        return outputs

    if abs(package["cStar"] - c_star) <= GUARD_CSTAR_RTOL * abs(package["cStar"]):
        print(f"    {name}: scan c* {c_star:.4f} matches the package's sonic throat {package['cStar']:.4f}")
        return outputs

    # The second hidden-defect audit's finding F1 (2026-09-28): the package's own rocket solver can converge to the
    # same downstream (larger p_c/p, lower rho*u) sonic point the tree used to, near a melting plateau with two
    # maxima. The scan's first maximum, upstream of it (a smaller p_c/p, so a larger ratio here) and with no more
    # mass flux (a c* no higher), is exactly the geometry the audit found and not a disagreement to guard against.
    package_ratio = 1.0 / package["pressureRatio"]
    if ratio > package_ratio and c_star <= package["cStar"]:
        print(f"    {name}: scan c* {c_star:.4f} at p/p_c {ratio:.6f}, upstream of the package's own sonic but "
             f"downstream throat {package['cStar']:.4f} at p/p_c {package_ratio:.6f} (finding F1)")
        outputs["packageRocketThroat"] = package
        return outputs

    raise RuntimeError(f"{name}: scan c* {c_star!r} against the package's sonic throat c* {package['cStar']!r}")


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


def _element_mixture_throats(writer: Writer, cases: list[tuple[str, list[str], list[float], float, float]]) -> None:
    """Plain elemental reactants at a fixed temperature, each pressure and enthalpy assigned directly (the
    reference-state enthalpy of plain elements is zero, so there is no propellant h0 to offset from). Shared by
    `element_mixture_throats` (finding F1) and `plateau_edge_throats` (finding F3): both are the same generation
    method over a differently-motivated case list."""
    for name, reactants, mass_fractions, chamber_pressure_pa, enthalpy in cases:
        weights = np.array(mass_fractions)
        temperatures = np.full(len(reactants), ELEMENT_MIXTURE_TEMPERATURE_K)
        reac, prod = make_mixtures(reactants)
        descriptions = describe_reactants(reactants, weights, temperatures)
        outputs = _throat_outputs(name, reac, prod, weights, temperatures, chamber_pressure_pa, enthalpy)
        writer.case(
            "throat", name,
            inputs=rocket_inputs(descriptions, prod.species_names, chamber_pressure_pa, enthalpy, FLOW_SHIFTING, False,
                                 extra={"enthalpyAssigned": True}),
            outputs=outputs, script_path=__file__, method="cea-package-mass-flux-scan")


def element_mixture_throats(writer: Writer) -> None:
    """The second hidden-defect audit's finding F1 (2026-09-28): plain elemental reactants, at the pressure and
    enthalpy the audit's own sweep found a second, larger-rho*u maximum downstream of the true one."""
    _element_mixture_throats(writer, ELEMENT_MIXTURE_CASES)


def plateau_edge_throats(writer: Writer) -> None:
    """The second hidden-defect audit's finding F3 (2026-09-28): representative points of the audit's own Li2O and
    BeO/H2O melting plateaus, where a re-solve unchecked against the bracket's subsonic fingerprint and u^2/a^2 < 1
    can land on the far (Mach >= 1) side of the edge."""
    _element_mixture_throats(writer, PLATEAU_EDGE_CASES)


def third_pass_reach_throats(writer: Writer) -> None:
    """The third audit pass of 2026-09-28 (part 2, finding 1): one fixture inside the 3 MPa Li2O band whose plateau
    edge the geometric-offset fix (Performance BOOT.md) reaches, where the eight linear steps of the bracket width
    it replaces did not."""
    _element_mixture_throats(writer, THIRD_PASS_REACH_CASES)


def generate(writer: Writer) -> None:
    ap_htpb_al_throats(writer)
    example13_throats(writer)
    element_mixture_throats(writer)
    plateau_edge_throats(writer)
    third_pass_reach_throats(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
