"""The reaction plateau of AP/HTPB/Al (2026-10-03, the StateRecord node's pinned set): hp and sp states inside and beside
the band where Al(OH)3(a), Al2O3(a) and H2O(L) stand together and fix the temperature independently of the pressure.

Neither the package nor the tree converges these states from a cold start: the package aborts on "Re-insertion of
AL2O3(a) likely to cause singular matrix" or ends at a few tens of kelvin, and the tree's cold iterate dives below
100 K before a condensed species enters. Both converge from a neighbour, so every case is solved after a tp state at
430 K on the same reactants, in one `EqSolution` (`cea_cases.solve_equilibrium`'s `seed_temperature`), and its inputs
carry that seed (`"seed": {"kind": "tp", "temperature": ...}`), which a consumer repeats.

The table, the reactants and the element moles are those of the tp fixture `ap-htpb-al_pc7MPa_T430` of
`retention_threshold.py` (`AP_HTPB_AL`, imported, never copied). The band is measured, not typed, between two
single-phase states on either side of the plateau temperature (415.948162 K at every pressure): the tp state at 416.0 K
above it, and the state at 415.9 K below it. The package does not converge a tp state below the plateau (the abort
above, from every start tried), so the lower end is the package's own hp state at 415.9 K, found by bisection on the
assigned enthalpy. A case sits at a fraction `f` of the interval between their enthalpies (and entropies), the fractions
below 0 and above 1 being single-phase states just outside it. At 0.1 MPa the gas holds the water and there is no
plateau.

On a plateau the package finds the constant-temperature derivative matrix singular, says so, and reports its frozen
heat capacities and derivatives; the six second-order fields of those fixtures are therefore the package's fallback and
not a reference (tests/Fixtures/BOOT.md, the `seeded` kind).
"""
from __future__ import annotations

import sys

import numpy as np

from cea_cases import describe_reactants, equilibrium_inputs, make_mixtures, solve_equilibrium
from retention_threshold import AP_HTPB_AL
from writer import Writer, main_of

MPA_TO_PA = 1.0e6

SEED_TEMPERATURE = 430.0
SEED = {"kind": "tp", "temperature": SEED_TEMPERATURE}

# The two single-phase states bounding the band (K): the plateau temperature lies between them.
BAND_LOW_TEMPERATURE = 415.9
BAND_HIGH_TEMPERATURE = 416.0
# The search for the lower end: the enthalpy steps down from the upper end's by this much (J/kg), at most this many
# times, until the state is below 415.9 K (a larger leap is a state the package does not converge); the interval that
# brackets 415.9 K is then halved this many times.
BAND_SEARCH_STEP = 2.0e4
BAND_SEARCH_LIMIT = 30
BAND_SEARCH_BISECTIONS = 50

PRESSURES_PA = [1.0 * MPA_TO_PA, 7.0 * MPA_TO_PA, 20.0 * MPA_TO_PA]
# Fractions of the band: three inside, one just below and one just above.
FRACTIONS = [0.1, 0.5, 0.9, -0.05, 1.05]
# The problem kinds and the key of their assigned property in a state record.
PROBLEMS = [("hp", "enthalpy"), ("sp", "entropy")]


def _band(solve) -> dict[str, tuple[float, float]]:
    """(lower, upper) of the enthalpy and of the entropy of the band; `solve(kind, value)` is one seeded solve."""
    high = solve("tp", BAND_HIGH_TEMPERATURE)
    upper = high["enthalpy"]
    for _ in range(BAND_SEARCH_LIMIT):
        lower = upper - BAND_SEARCH_STEP
        if solve("hp", lower)["temperature"] < BAND_LOW_TEMPERATURE:
            break
        upper = lower
    else:
        raise RuntimeError("no state below the band within the search limit")
    for _ in range(BAND_SEARCH_BISECTIONS):
        middle = 0.5 * (lower + upper)
        if solve("hp", middle)["temperature"] < BAND_LOW_TEMPERATURE:
            lower = middle
        else:
            upper = middle
    low = solve("hp", upper)
    return {key: (low[key], high[key]) for _, key in PROBLEMS}


def _case_name(pressure_pa: float, kind: str, fraction: float) -> str:
    return f"ap-htpb-al-reaction-plateau_p{pressure_pa / MPA_TO_PA:g}MPa_{kind}{fraction:g}"


def generate(writer: Writer) -> None:
    reac, prod = make_mixtures(AP_HTPB_AL["reactants"])
    weights = np.array(AP_HTPB_AL["massFractions"])
    temperatures = np.array(AP_HTPB_AL["temperatures"])
    descriptions = describe_reactants(AP_HTPB_AL["reactants"], weights, temperatures)
    for pressure_pa in PRESSURES_PA:
        def solve(kind: str, value: float, pressure_pa: float = pressure_pa) -> dict:
            return solve_equilibrium(reac, prod, weights, kind, value, pressure_pa, transport=False,
                                     seed_temperature=SEED_TEMPERATURE)

        band = _band(solve)
        for kind, key in PROBLEMS:
            low, high = band[key]
            for fraction in FRACTIONS:
                value = low + fraction * (high - low)
                writer.case(
                    "seeded", _case_name(pressure_pa, kind, fraction),
                    inputs=equilibrium_inputs(descriptions, prod.species_names, kind, value, pressure_pa, False,
                                              extra={"seed": SEED}),
                    outputs=solve(kind, value), script_path=__file__)


if __name__ == "__main__":
    sys.exit(main_of(generate))
