"""The second hidden-defect audit's finding F1 (2026-09-28, Equilibrium BOOT.md, the two-stage retention
threshold): tp fixtures at conditions where a trace gaseous carrier sits on the single, wrong threshold of
1e-8 and the reference's own two-stage rule (tsize/xsize) does not.

RP-1311 example 5's table (ammonium perchlorate propellant) at 300 K, 1 bar and 70 bar, and at 305 K, 1 MPa:
the audit's own example of the crossing failure ("RP-1311 example 5 at 300 K, 1 bar").

A NaClO4 decomposition and AP/HTPB/Al at 420-430 K were also asked for (the orchestrator's task) and were tried
here; both are recorded as not converging by the current equilibrium node and are not committed (Equilibrium
BOOT.md, the F1/F5 criterion's escalation note): NaClO4 (Na:Cl:O = 1:1:4 from pure elements) reduces almost
entirely to NaCL(cr) + O2 at 500 K and 800 K, 1 bar, so the all-gaseous cold-start trial never converges and the
condensed inclusion test (run only after a converged gaseous trial) never gets a chance to run; AP/HTPB/Al at
7 MPa/430 K and 1 MPa/420 K converges repeatedly at the Newton level but the exit guard
(`CondensedSet.ExitGuardFindsAPositiveCandidate`) finds a stood-down candidate still showing a positive inclusion
gain after several condensed-set changes settle, which is a different, deeper gap than the two fixed this audit
(BOOT.md's escalation note has the diagnostic evidence).
"""
from __future__ import annotations

import sys

from cea_cases import describe_reactants, equilibrium_inputs, make_mixtures, solve_equilibrium
from rp1311 import EXAMPLE5_OMIT, EXAMPLE5_REACTANTS, EXAMPLE5_WEIGHTS
from writer import Writer, main_of

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


def generate(writer: Writer) -> None:
    _rp1311_example5(writer)


if __name__ == "__main__":
    sys.exit(main_of(generate))
