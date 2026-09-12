"""Case builders over the reference package: reactant descriptions, equilibrium and rocket solves, station records in SI.

Field names follow the tree's result types (MixtureState, PerformanceFigures, TransportFigures).
Two fields are derived here from the package's own outputs rather than read from it, because
the package does not expose them while the tree reports them:

    dlnVdlnP = -(cp_eq / cv_eq) / gamma_s                       (RP-1311 eq. 2.74 solved for the derivative)
    dlnVdlnT = sqrt(-(cp_eq - cv_eq) * dlnVdlnP * M / R)         (RP-1311 eq. 2.72 solved for the derivative)

and the sound speed of an equilibrium (non-rocket) state is sqrt(gamma_s R T / M), the
relation the package itself uses for its rocket stations.
"""
from __future__ import annotations

import math

import numpy as np

import cea

from common import (BAR_TO_PA, KJ_TO_J, MILLIPOISE_TO_PA_S, MW_PER_CM_K_TO_W_PER_M_K,
                    atomic_weight, element_moles, read_thermo)

RECORDS = read_thermo()

CAL_TO_J = 4.184   # thermochemical calorie, the unit the package's custom reactants take

FLOW_SHIFTING = "shiftingEquilibrium"
FLOW_FROZEN_CHAMBER = "frozenAtChamber"
FLOW_FROZEN_THROAT = "frozenAtThroat"
N_FRZ = {FLOW_SHIFTING: None, FLOW_FROZEN_CHAMBER: 1, FLOW_FROZEN_THROAT: 2}

EQ_TYPES = {"tp": cea.TP, "hp": cea.HP, "sp": cea.SP}


class Custom:
    """A reactant that is not a thermo.inp record: formula, assigned enthalpy (cal/mol as the package takes it), temperature."""

    def __init__(self, name: str, formula: dict[str, float], enthalpy_cal_per_mol: float,
                 temperature: float = 298.15, note: str | None = None) -> None:
        self.name = name
        self.formula = formula
        self.enthalpy_cal_per_mol = enthalpy_cal_per_mol
        self.temperature = temperature
        self.note = note
        self.molar_mass = sum(count * atomic_weight(RECORDS, symbol) for symbol, count in formula.items())

    def to_cea(self) -> cea.Reactant:
        return cea.Reactant(name=self.name, formula=self.formula, molecular_weight=self.molar_mass,
                            enthalpy=self.enthalpy_cal_per_mol, enthalpy_units="cal/mol", temperature=self.temperature)


def describe_reactants(reactants: list, weights, temperatures=None) -> list[dict]:
    """Reactant descriptions with mass fractions (weights normalized) and temperatures, custom reactants spelled out."""
    weights = np.asarray(weights, dtype=float)
    fractions = weights / weights.sum()
    if temperatures is None:
        temperatures = [None] * len(reactants)
    elif np.isscalar(temperatures):
        temperatures = [float(temperatures)] * len(reactants)
    out = []
    for r, w, t in zip(reactants, fractions, temperatures):
        if isinstance(r, Custom):
            out.append({
                "name": r.name, "custom": True, "formula": {k.upper(): v for k, v in r.formula.items()},
                "molarMass": r.molar_mass, "enthalpy": r.enthalpy_cal_per_mol * CAL_TO_J,
                "temperature": r.temperature, "massFraction": float(w), "note": r.note,
            })
        else:
            out.append({"name": r, "massFraction": float(w), "temperature": None if t is None else float(t)})
    return out


def make_mixtures(reactants: list, products: list[str] | None = None, omit: list[str] | None = None):
    species = [r.to_cea() if isinstance(r, Custom) else r for r in reactants]
    reac = cea.Mixture(species)
    if products is not None:
        prod = cea.Mixture(products)
    elif omit:
        prod = cea.Mixture(species, products_from_reactants=True, omit=omit)
    else:
        prod = cea.Mixture(species, products_from_reactants=True)
    return reac, prod


def _derivatives(cp_eq: float, cv_eq: float, gamma_s: float, molar_mass: float, frozen: bool) -> tuple[float, float]:
    """(dlnV/dlnT)_p and (dlnV/dlnP)_T; a frozen composition has the ideal-gas values 1 and -1."""
    if frozen or cv_eq <= 0.0 or gamma_s <= 0.0:
        return 1.0, -1.0
    dlnv_dlnp = -(cp_eq / cv_eq) / gamma_s
    dlnv_dlnt = math.sqrt(max(0.0, -(cp_eq - cv_eq) * dlnv_dlnp * molar_mass / cea.R))
    return dlnv_dlnt, dlnv_dlnp


def _state(get, transport: bool, frozen: bool = False) -> dict:
    """Common state fields; `get(name)` returns the package's value of a property at one station."""
    cp_eq = float(get("cp_eq")) * KJ_TO_J
    cv_eq = float(get("cv_eq")) * KJ_TO_J
    gamma_s = float(get("gamma_s"))
    molar_mass = float(get("M"))
    dlnv_dlnt, dlnv_dlnp = _derivatives(cp_eq, cv_eq, gamma_s, molar_mass, frozen)
    d = {
        "temperature": float(get("T")),
        "pressure": float(get("P")) * BAR_TO_PA,
        "density": float(get("density")),
        "enthalpy": float(get("enthalpy")) * KJ_TO_J,
        "internalEnergy": float(get("energy")) * KJ_TO_J,
        "entropy": float(get("entropy")) * KJ_TO_J,
        "gibbsEnergy": float(get("gibbs_energy")) * KJ_TO_J,
        "molarMass": molar_mass,
        "gasMolarMass": float(get("MW")),
        "cpFrozen": float(get("cp_fr")) * KJ_TO_J,
        "cpEquilibrium": cp_eq,
        "cvFrozen": float(get("cv_fr")) * KJ_TO_J,
        "cvEquilibrium": cv_eq,
        "gammaS": gamma_s,
        "dlnVdlnT": dlnv_dlnt,
        "dlnVdlnP": dlnv_dlnp,
    }
    if transport:
        d.update({
            "viscosity": float(get("viscosity")) * MILLIPOISE_TO_PA_S,
            "frozenConductivity": float(get("conductivity_fr")) * MW_PER_CM_K_TO_W_PER_M_K,
            "reactingConductivity": float(get("conductivity_eq")) * MW_PER_CM_K_TO_W_PER_M_K,
            "frozenPrandtl": float(get("Pr_fr")),
            "reactingPrandtl": float(get("Pr_eq")),
        })
    return d


def equilibrium_state(solution: cea.EqSolution, transport: bool) -> dict:
    d = _state(lambda name: getattr(solution, name), transport)
    d["soundSpeed"] = math.sqrt(d["gammaS"] * cea.R * d["temperature"] / d["molarMass"])
    d["moleFractions"] = {name: float(x) for name, x in solution.mole_fractions.items()}
    d["converged"] = bool(solution.converged)
    return d


def rocket_station(solution: cea.RocketSolution, i: int, label: str, transport: bool, frozen: bool) -> dict:
    d = {"station": label, "index": i, "frozen": frozen}
    d.update(_state(lambda name: getattr(solution, name)[i], transport, frozen))
    d.update({
        "soundSpeed": float(solution.sonic_velocity[i]),
        "mach": float(solution.Mach[i]),
        "areaRatio": float(solution.ae_at[i]),
        "pressureRatio": float(solution.P[0] / solution.P[i]),
        "characteristicVelocity": float(solution.c_star[i]),
        "thrustCoefficient": float(solution.coefficient_of_thrust[i]),
        "specificImpulse": float(solution.Isp[i]),
        "vacuumSpecificImpulse": float(solution.Isp_vacuum[i]),
    })
    d["moleFractions"] = {name: float(x[i]) for name, x in solution.mole_fractions.items()}
    return d


def solve_equilibrium(reac, prod, weights, kind: str, value_si: float, pressure_pa: float,
                      transport: bool, trace: float | None = None) -> dict:
    """tp: value is T in K; hp: h in J/kg; sp: s in J/(kg K). Returns the state record in SI."""
    options = {"transport": transport}
    if trace is not None:
        options["trace"] = trace
    solver = cea.EqSolver(prod, reactants=reac, **options)
    solution = cea.EqSolution(solver)
    state1 = value_si if kind == "tp" else value_si / cea.R
    solver.solve(solution, EQ_TYPES[kind], state1, pressure_pa / BAR_TO_PA, weights)
    if not solution.converged:
        raise RuntimeError(f"{kind} solve did not converge (last_error {solution.last_error})")
    return equilibrium_state(solution, transport)


def solve_rocket(reac, prod, weights, temperatures, chamber_pressure_pa: float, flow: str, transport: bool,
                 area_ratios=None, pressure_ratios=None, subsonic_area_ratios=None):
    """Solves the infinite-area-chamber rocket problem; returns (solution, reactant enthalpy in J/kg)."""
    solver = cea.RocketSolver(prod, reactants=reac, transport=transport)
    solution = cea.RocketSolution(solver)
    enthalpy = float(reac.calc_property(cea.ENTHALPY, weights, temperatures))   # J/kg
    solver.solve(solution, weights, chamber_pressure_pa / BAR_TO_PA, pi_p=pressure_ratios,
                 subar=subsonic_area_ratios, supar=area_ratios, iac=True, hc=enthalpy / cea.R, n_frz=N_FRZ[flow])
    if not solution.converged:
        raise RuntimeError(f"rocket solve did not converge (last_error {solution.last_error})")
    return solution, enthalpy


def station_labels(solution: cea.RocketSolution) -> list[str]:
    labels = ["chamber", "throat"]
    for i in range(2, solution.num_pts):
        subsonic = solution.Mach[i] < 1.0
        labels.append(("subsonicExit" if subsonic else "exit") + f"{i - 1}")
    return labels


def rocket_outputs(solution: cea.RocketSolution, transport: bool, flow: str = FLOW_SHIFTING) -> dict:
    """The stations in the package's order; stations from the freezing point on are flagged frozen."""
    labels = station_labels(solution)
    frozen_from = N_FRZ[flow]
    return {"stations": [rocket_station(solution, i, labels[i], transport, frozen_from is not None and i >= frozen_from)
                         for i in range(solution.num_pts)]}


def rocket_inputs(descriptions: list[dict], products: list[str], chamber_pressure_pa: float, reactant_enthalpy: float,
                  flow: str, transport: bool, area_ratios=None, pressure_ratios=None, subsonic_area_ratios=None,
                  of_ratio: float | None = None, omit: list[str] | None = None, trace: float | None = None) -> dict:
    return {
        "reactants": descriptions,
        "oxidizerToFuelRatio": of_ratio,
        "elementMoles": element_moles(RECORDS, descriptions),
        "chamberPressure": chamber_pressure_pa,
        "reactantEnthalpy": reactant_enthalpy,
        "areaRatios": list(area_ratios or []),
        "pressureRatios": list(pressure_ratios or []),
        "subsonicAreaRatios": list(subsonic_area_ratios or []),
        "flow": flow,
        "transport": transport,
        "products": list(products),
        "omit": list(omit or []),
        "trace": trace,
    }


def equilibrium_inputs(descriptions: list[dict], products: list[str], kind: str, value_si: float, pressure_pa: float,
                       transport: bool, of_ratio: float | None = None, omit: list[str] | None = None,
                       trace: float | None = None, derived_from: dict | None = None, extra: dict | None = None) -> dict:
    key = {"tp": "temperature", "hp": "enthalpy", "sp": "entropy"}[kind]
    d = {
        "reactants": descriptions,
        "oxidizerToFuelRatio": of_ratio,
        "elementMoles": element_moles(RECORDS, descriptions),
        key: value_si,
        "pressure": pressure_pa,
        "transport": transport,
        "products": list(products),
        "omit": list(omit or []),
        "trace": trace,
    }
    if derived_from is not None:
        d["derivedFrom"] = derived_from
    if extra:
        d.update(extra)
    return d


def derive_equilibrium_cases(writer, script_path: str, base_name: str, solution: cea.RocketSolution, reac, prod,
                             weights, descriptions: list[dict], transport: bool, indices: list[int],
                             of_ratio: float | None = None) -> None:
    """tp, hp and sp cases at the given rocket stations, solved afresh with the equilibrium solver."""
    labels = station_labels(solution)
    for i in indices:
        pressure_pa = float(solution.P[i]) * BAR_TO_PA
        values = {
            "tp": float(solution.T[i]),
            "hp": float(solution.enthalpy[i]) * KJ_TO_J,
            "sp": float(solution.entropy[i]) * KJ_TO_J,
        }
        for kind, value in values.items():
            outputs = solve_equilibrium(reac, prod, weights, kind, value, pressure_pa, transport)
            writer.case(
                kind, f"{base_name}_{labels[i]}",
                inputs=equilibrium_inputs(descriptions, prod.species_names, kind, value, pressure_pa, transport,
                                          of_ratio=of_ratio,
                                          derived_from={"case": base_name, "station": labels[i], "index": i,
                                                        "temperature": float(solution.T[i])}),
                outputs=outputs,
                script_path=script_path,
            )
