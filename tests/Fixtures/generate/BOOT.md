# BOOT.md — generate

## Purpose

The Python scripts that produce the reference fixtures of the Fixtures node: the cases
of the case matrix solved with the `cea` package (rocket and equilibrium problems),
values evaluated independently of both the package and the tree (the thermodynamic
functions and the transport fits, read from the committed NASA files), and the
package's gas constant. A node of its own because it is code, run in its own Python
environment, while the parent holds data files and a .NET loader.

## Invariants

- **Deterministic output.** The same scripts, package and data files produce
  byte-identical fixtures: the writer compares before writing and never rewrites a
  document whose regenerated content differs only in the date, so `generatedOn` is the
  day the content last changed.
- **The independent scripts share nothing with what they verify.** `thermo_functions.py`
  and `transport_fits.py` read the NASA files with the generator's own reader
  (`common.py`) and spell the formulas out in their general form; they do not import the
  package's solvers and cannot import the tree.
- **One writer.** Only `writer.py` writes files, and every document it writes carries
  the provenance the parent requires.
- **Inputs suffice to reproduce a case without its script**: reactants with mass
  fractions and temperatures (custom reactants spelled out with formula, molar mass and
  enthalpy), element moles per kilogram, the product species list the package used,
  the problem values in SI, and for derived cases the station they come from.
- **No fixture value is typed into a script.** Values come from the package or from
  evaluating the files; what is typed is the case matrix (inputs), as this node's
  Constraints define it (the last bullet of that section).
- **A case that does not converge stops the run.** Nothing is written for it; a
  fixture never carries an unconverged result.

## Dependencies

None.

Outside the tree: Python 3.11+, `cea` 3.3.4 and `numpy` 2.5.3 (`requirements.txt`;
the environment lives in `.venv`, ignored by git); the tree's `data/thermo.inp` and
`data/trans.inp`, read as files.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and the root. In addition:

- Layout: one module per fixture family (`constants.py`, `thermo_functions.py`,
  `transport_fits.py`, `rp1311.py`, `propellants.py`, `plateaus.py`, `throat_scan.py`
  since 2026-09-27), the case builders over the
  package in `cea_cases.py`, shared helpers in `common.py`, the writer in `writer.py`,
  the document comparison of the sampled check in `document_comparison.py` (2026-09-30),
  the driver `regenerate.py`. Every family module exposes `generate(writer)` and runs
  standalone.
- Unit conversion happens only here, through the named factors of `common.py`: bar to
  Pa, kJ to J, millipoise to Pa·s, mW/(cm·K) to W/(m·K), micropoise to Pa·s,
  μW/(cm·K) to W/(m·K). The package's units were established by a probe against known
  magnitudes before the factors were written.
- The package takes assigned enthalpies of custom reactants in cal/mol; the fixture
  records them in J/mol (thermochemical calorie, 4.184 J).
- Frozen rocket cases are generated without transport: the package's frozen expansion
  with transport on fails for product sets of about a hundred species (recorded in the
  case matrix below).
- `throat_scan.py` (2026-09-27) builds the throat family by the method and the guard
  the case matrix below states. It reuses the reactant lists and compositions of
  `plateaus.py` and `rp1311.py` by import, never by copy. The scan solves only through
  `cea_cases.solve_equilibrium`, and the guard's rocket solves only through
  `cea_cases.solve_rocket`.
- `generatorSha256` (2026-09-27) is the SHA-256 of the concatenation, over every `*.py`
  file of this directory and `requirements.txt` in ordinal order of their names, of the
  file name, a LF, and the file's bytes with CRLF normalized to LF. The script hashes
  use the same normalization, so that a Windows checkout and a Linux one agree. The
  fixtures node's tests recompute both from the committed files.
- JSON form: indent 2, LF, UTF-8, `NaN` forbidden, floats in Python's shortest
  round-trip form, numpy values converted to Python numbers.

The standalone sweep of a family script (moved here from the parent's Procedure on
2026-10-01: only this directory's scripts are bound by it; the parent's
[BOOT.md](../BOOT.md) keeps a pointer):

  ⚠ 2026-09-27: "sweeps only the kinds it produces" reads as if a standalone run were
  always safe, but a kind directory several scripts share (`tp`, `hp`, `sp` and
  `rocket` are each written by `propellants.py`, `rp1311.py`, `plateaus.py` and, for
  `tp`, `low_temperature.py` and `condensed_phase_limit.py` too) has no script that
  "produces" it alone: a standalone run of one script still sweeps that whole
  directory, since the sweep only knows the paths its own run wrote, and removes or
  reports as stale every fixture the *other* scripts placed there. Found by the coder
  of this date: `python propellants.py` alone removed 93 committed fixtures of `rp1311.py`,
  `plateaus.py`, `low_temperature.py` and `condensed_phase_limit.py` and rewrote more
  (`git status`, immediately reverted with `git checkout --`). A standalone run is safe
  only for a kind exactly one script writes (`constants`, `thermo`, `transport`,
  `throat`, `reactant`); regenerating a shared kind, or committing any change to a script that
  writes one, goes through the full `regenerate.py` driver, never a single family
  script alone. `reactant` is written only by `propellants.py`, but that script also writes
  the shared `tp`/`hp`/`sp`/`rocket` kinds, so it is regenerated through `regenerate.py`
  scoped to the one kind it owns alone (`python regenerate.py reactant`) rather than
  standalone, when only that kind needs a new case.

The case matrix (moved here from the parent's Constraints on 2026-10-01: only the
generator's scripts are bound by it; the parent's [BOOT.md](../BOOT.md) lists the
families):

- Case matrix of version 1:
  - RP-1311 examples 1 (tp), 3 (hp, two fuels), 5 (hp, solid with a custom binder and
    condensed products), 8 (rocket LOX/LH2), 12 (rocket MMH/NTO, shifting and frozen
    with freezing at the throat), 14 (tp at low temperature with condensation), each
    from the package's own `rp1311` sample scripts, unchanged in inputs.
  - LOX/LH2 (`O2(L)` at 90.17 K, `H2(L)` at 20.27 K): O/F 4.0, 5.0, 6.0, 7.0, 8.0;
    chamber pressure 5, 7, 10 MPa; area ratios 20 and 77.5 in one case; shifting,
    frozen at chamber, frozen at throat; transport on for shifting cases.
  - LOX/RP-1 (`O2(L)` at 90.17 K, `RP-1` at 298.15 K): O/F 2.0, 2.3, 2.6, 2.9, 3.2; 7
    and 10 MPa; area ratios 16 and 40; shifting and frozen at throat; transport on for
    shifting cases.
  - NTO/UDMH (`N2O4(L)`, `C2H8N2(L),UDMH`, both at 298.15 K): O/F 1.8, 2.0, 2.2, 2.4,
    2.6; 1 and 2 MPa; area ratios 10 and 50; shifting and frozen at throat; transport
    on for shifting cases.
  - AP/HTPB/Al (`NH4CLO4(I)` 68 %, HTPB 14 %, `AL(cr)` 18 % by mass, all at
    298.15 K): 5 and 7 MPa; area ratios 8 and 12; shifting; transport on; condensed
    products expected (`AL2O3(L)` in the chamber). HTPB is not a `thermo.inp` record:
    its definition is that of Thomas and Petersen (AIAA Journal, 2021,
    doi:10.2514/1.J060972, IPDI-cured R-45M: C 213.8 H 323.0 O 4.6 N 2.3, +342 kJ/mol of
    that unit at 298.15 K), the owner's decision of 2026-10-02, given to the package as
    342000 / 4.184 cal/mol.

    ⚠ 2026-10-02: was the unsourced provisional definition (C 7.3165 H 10.3416 O 0.0674,
    −250 cal/mol), now the cited one → [ACCEPTANCE.md](../ACCEPTANCE.md)
  - Melting-plateau cases, added by the design session of 2026-09-13 (the
    condensed-phase rules of the equilibrium node):
    - RP-1311 example 13 (N2H4/Be 80/20 at O/F 0.4925…, 20.68 MPa; exits `p_c/p` 3,
      10, 30, 300), generated with the package's `insert` list seeding `BeO(L)`, the
      list recorded in the inputs: its throat and first exit sit on the BeO melting
      plateau at 2851 K, which the package only converges with the insert.
    - Direct plateau stations of AP/HTPB/Al at 7 MPa: single-exit rocket cases
      (chamber plus one exit each) at pressure ratios covering the AL2O3(a)/(L)
      plateau and both its edges (`p_c/p` 21.6 … 37.4), so the pinned pair, its
      crossing temperature, the plateau `γ_s` and sound speed have fixtures;
      single-exit because of the guard above.

      ⚠ 2026-10-02: "both its edges" held for the provisional HTPB. With the cited one
      (+25 K in the chamber) `p_c/p` 21.6 and 23 are single-phase liquid (the plateau's
      upper-temperature edge), 25 to 36 sit on the pinned pair at 2327 K, and 37.4 is
      still on it (`AL2O3(a)` 0.0745, `AL2O3(L)` 0.0123 kg per kg): the pure `AL2O3(a)`
      edge has no fixture until the ratios are extended, a design decision not taken
      by the regeneration that moved them.
    - The fuel-rich chamber, AP/HTPB/Al at O/F 0.50 and 7 MPa, hp: the
      include/remove cycle case (`AL4C3(cr)`), which the package converges.
    - hp across the plateau: AP/HTPB/Al at 700 kPa, assigned enthalpies stepping
      through the AL2O3 latent-heat band, one case per enthalpy; the inputs mark
      them `enthalpyAssigned` (with the band's edge enthalpies), so the propellant
      tests know the assigned value is not the reactants' enthalpy.
    - tp at transition bounds: AP/HTPB/Al at 2327 K, example 13's mixture at 2851 K
      and 2373 K, one case each, so the record chosen exactly at a bound has a
      fixture.
    - No fixture inside the `ALN(L)` 2700 K gap: the package cannot converge there
      (its own hp fails between the gap's edges), so the tree's pinned pair at the
      crossing, once the record is cut, is verified by the tree's own invariant
      tests, not against the reference.
    On a pinned two-phase station the package reports `cp_eq = 0` and the
    `cea_cases.py` derivations of the equilibrium derivatives degenerate; the
    generator writes the reference's plateau convention directly — `cpEquilibrium`,
    `cvEquilibrium` and `dlnVdlnT` zero, `dlnVdlnP = −1/γ_s` — instead of deriving
    them (RP-1311 section 3.5; the equilibrium node writes the same zeros).

  ⚠ 2026-09-12: the frozen cases were to carry transport too. With transport on, the
  package's frozen expansion reports "Frozen calculations did not converge in 8
  iterations" for product sets of about a hundred species and more (124 for LOX/RP-1,
  161 for NTO/UDMH), while the same cases converge without transport and LOX/LH2 (11
  species) converges either way. Frozen cases are therefore generated without
  transport; found by the generator's convergence check.

  - Equilibrium-only cases derived from the stations of one shifting case per
    propellant (LOX/LH2 O/F 6 at 7 MPa, LOX/RP-1 O/F 2.6 at 10 MPa, NTO/UDMH O/F 2.2
    at 2 MPa, AP/HTPB/Al at 7 MPa) and of RP-1311 examples 8 (chamber, throat and the
    supersonic exits) and 12 (chamber and throat, its exits being frozen): a tp case at
    the station's (T, p), an hp case at (h, p), an sp case at (s, p), each solved
    afresh with the package's equilibrium solver, so that `Equilibrium` is verified
    without the nozzle; generated without transport.

  ⚠ 2026-09-12: the derived cases carried transport (the propellant ones inheriting it
  from the rocket case). Dropped for the reason above; mixture transport is verified on the
  rocket stations, which keep it.

  ⚠ 2026-09-12: stood "derived from every rocket station", which would be about two
  thousand files for the same coverage of the tp, hp and sp paths; narrowed to one
  case per propellant plus the two rocket examples when the generator was written.

  - A sodium case (2026-09-27): NaNO3(a) with RP-1, both at 298.15 K, O/F 4, 7 MPa,
    one hp case. It holds the six-interval `NaCN(II)` among its products, which the
    thermo node refused until that day (its `BOOT.md`).
  - Rules A and B (2026-09-28, `generate/retention_threshold.py`; the orchestrator's
    investigation 6, Equilibrium `BOOT.md`, "Two rules come before the remedies
    above"): four tp cases the reference converges but that the equilibrium node
    reached `SingularMatrix` on before the two rules.
    - NaClO4 and KClO4, Na (or K) : Cl : O = 1 : 1 : 4, fed to the package as three
      pure-element `Custom` reactants at the salt's own mole ratio (neither salt is
      a `thermo.inp` reactant), at 500 K and 800 K, 1 bar; the reference reduces
      both almost entirely to `O2` + `NaCL(cr)`/`KCL(cr)`.
    - AP/HTPB/Al at 7 MPa/430 K and 1 MPa/420 K, on the same reactants, mass
      fractions and product table as the `ap-htpb-al` chamber fixture (imported from
      `propellants.py`, not copied): a direct tp solve, not a rocket station.
  - `throat` (2026-09-27): the chamber and the throat of a shifting-equilibrium
    rocket, with no exit, where the throat is the first local maximum of the mass
    flux `ρu` met from the chamber along the chamber isentrope, found over the
    package's own sp solves and never taken from its rocket solver. It exists because
    the package's rocket solver reports a wrong throat at the high-pressure edge of a
    melting plateau, while its equilibrium solves there are sound. The performance
    node's `BOOT.md` states the defect, and RP-1311 sections 6.3.3 and 6.3.4 define
    the throat this family computes.
    - Cases, the enthalpy assigned relative to the reactants' own `h₀` unless stated:
      - AP/HTPB/Al of the plateau cases above at 7 MPa, `h₀` − 2.20, − 2.225,
        − 2.25, − 2.275 and − 2.30 MJ/kg;
      - the same at 1, 3 and 15 MPa, `h₀` − 2.25 MJ/kg;
      - RP-1311 example 13's propellant at 5 MPa, `h₀` and `h₀` + 250 kJ/kg, with
        its trace threshold;
      - the second hidden-defect audit's finding F1 (2026-09-28): AP/HTPB/Al at
        7 MPa, `h₀` − 2.625 MJ/kg, where the true (first, upstream) maximum sits on
        the pinned `AL2O3(a)`/`AL2O3(L)` pair itself, with a second, larger-`ρu`
        maximum further downstream that the tree used to return; a lean Al/O/H
        mixture (mass fractions 0.08/0.62/0.30) at 7 MPa and 1.9125 MJ/kg (reference
        state, not an offset); a B/O/H mixture forming `B2O3` (0.10/0.55/0.35) at
        0.3 MPa and −7.775 MJ/kg; a Li/F/H mixture forming `LiF` (0.08/0.62/0.30) at
        7 MPa and −7.575 MJ/kg. The last three reproduce, by element and mass
        fraction, the audit's own scratch harness
        (`scratchpad/audit2/harness/pt/performance/ZzAuditThroatSweep.cs`) and its
        sweep's own reproducing points
        (`scratchpad/audit2/out/pt/throat-sweep-misc.csv`).
      ⚠ 2026-10-02: the offsets `h₀` − 2.20 to − 2.30 MJ/kg were tuned to the provisional
      HTPB's `h₀`. With the cited one the 7 MPa throat of `h₀` − 2.25 MJ/kg sits at
      2356 K with only `AL2O3(L)` (the chamber at 2562 K), off the plateau, and the
      `h₀` − 2.625 MJ/kg case now has its throat on the pinned pair (2327 K, `AL2O3(a)`
      0.0593, `AL2O3(L)` 0.0276). The fixtures are the regenerated ones; the figures of
      the "Measured" bullets below belong to the provisional definition. Re-tuning the
      offsets to the plateau is a design decision.
    - Method, per case:
      1. The chamber is the package's hp at the assigned enthalpy and `p_c`.
      2. `ρu` is evaluated on 101 pressure ratios `p/p_c` evenly from 0.45 to 0.70,
         each an sp solve at the chamber's entropy with `u = √(2(h_c − h))`. The
         first local maximum met scanning from the high-pressure (chamber) end
         toward the low-pressure end must lie strictly inside the grid, or the run
         stops (2026-09-28, finding F1: earlier the scan took the grid's overall
         largest, which is the same point away from a plateau but the wrong,
         second/downstream one on one).
      3. The bracket of its two neighbours is refined by ternary search on `ρu` for
         50 steps.
      4. The throat is the sp solve at the bracket's high-pressure end, the chamber
         side. At a plateau edge that is the single-phase state, as the performance
         node defines it; elsewhere the two ends agree to rounding.
      5. The throat's figures: pressure ratio `p_c/p`, `c* = p_c/(ρu)`, velocity and
         specific impulse `u`, thrust coefficient `u/c*`, area ratio 1, Mach `u/a`
         with the equilibrium sound speed.
    - A guard proves the method on every case before it is written. The package's
      rocket solver runs the same case. Where its throat is sonic (`|Mach − 1|` ≤
      1e-4), the scan's c* must equal the package's within 1e-5 relative, or the run
      stops. Where it is not, the generator logs both c* values, and the fixture records
      the package's throat under `outputs.packageRocketThroat` (c*, Mach, pressure
      ratio) for the record. No test compares with that object.

      ⚠ 2026-09-28, finding F1: the guard's only accepted disagreement used to be "the
      package's own throat is not sonic". The four new cases' own package rocket
      solver converges to the same wrong, second/downstream sonic point the tree used
      to return, so it reports a throat that is sonic but not equal to the scan's. A
      second accepted branch: when the scan's ratio `p/p_c` is above the package's own
      (upstream of it, since a smaller `p/p_c` is downstream) and the scan's c* is no
      higher than the package's, the disagreement is exactly this known divergence,
      not a defect of the method; the package's throat is still recorded under
      `outputs.packageRocketThroat` for the record.
    - Measured 2026-09-26/27 with the scratch versions of this method:
      - AP/HTPB/Al at 7 MPa, `h₀` − 2.20, − 2.225, − 2.30 MJ/kg: 1336.537, 1333.066
        and 1333.226 m/s, equal to the package's printed c* to 0.001 m/s;
      - `h₀` − 2.25 and − 2.275: 1330.444 and 1330.435 m/s, where the package prints
        1411.722 and 1359.032;
      - example 13 at 5 MPa, `h₀`: 1957.753 m/s on the BeO plateau, the package
        1957.755, both at Mach 1;
      - `h₀` + 250 kJ/kg: 1941.006 m/s at `p/p_c` 0.612894, where `u²/a²` is 0.880
        on the chamber side and 1.014 on the plateau. The package prints 1949.759 at
        Mach 0.9335, solved at `p/p_c` 0.613466.
    - Measured 2026-09-28 for the four F1 cases (the generator's own run):
      `h₀` − 2.625 MJ/kg gives 1356.2237 m/s at `p/p_c` 0.606665, upstream of and no
      less than the package's own sonic, downstream throat of 1358.2296 m/s at
      `p/p_c` 0.554635; the lean Al/O/H, B2O3 and LiF mixtures reproduce the audit's
      own sweep figures within its own tolerance, each accepted by the same branch.
    - The document is that of the rocket kind, with `stations` holding the chamber
      and the throat only, and `inputs` marking `enthalpyAssigned` as the hp band
      cases do (the reference-state cases carry the flag `true` too: the assigned
      enthalpy equals the offset since `h₀ = 0` there). Frozen flow is not generated:
      a frozen throat has no plateau.
  - `thermo`: `Cp°/R`, `H°/RT`, `S°/R`, `G°/RT` for the species listed in
    `thermo_functions.py` (gaseous and condensed records, one with four intervals), at
    those of 200, 298.15, 500, 1000, 1000.0001, 2000, 3000, 5000, 6000 K that lie in the
    record's range, the record's first bound, midpoint and last bound (so that a narrow
    condensed record such as `ALCL3(cr)`, 300–465.7 K, still has three points inside),
    plus one point 5 % below the first bound and one 5 % above the last, flagged out of
    range; from an independent Python evaluation of the polynomials read
    from `data/thermo.inp` (not through the package), plus the package's `R` in `constants`.

  ⚠ 2026-09-12: stood "at 200, 298.15, …, 6000 K" for every species. Far outside a
  fit the polynomial cancels catastrophically and two evaluations would compare
  rounding, not formulas; the out-of-range points are kept close to the bounds.

  - `transport`: pure-species and pair fit values for the species and pairs listed in
    `transport_fits.py`, at the fit bounds and at those of 300, 500, 1000, 2000, 3000,
    5000 K inside each fit, from an independent Python evaluation of `data/trans.inp`;
    mixture transport at stations is part of the rocket and derived equilibrium fixtures
    generated with transport on.

## Acceptance criteria

- [x] 2026-09-12 — `regenerate.py` produces the seven kinds of the case matrix and
      `regenerate.py --check` exits 0 immediately afterwards; a fixture altered by hand
      is reported as `changed` with exit 1, a deleted one as `missing`, an extra file as
      `stale` (run by hand on the reference machine; the run printed 261 fixtures).

      ⚠ 2026-09-27: "the seven kinds" is now eight — `throat_scan.py` added the `throat`
      kind (the case matrix in this node's Constraints). `regenerate.py --check` exits 0 on the current
      tree, 327 fixtures, none stale or missing.

      ⚠ 2026-09-27, the sodium case (the case matrix in this node's Constraints): `propellants.py` gains
      `sodium_hp`, one hp file. `regenerate.py --check` exits 0 on the current tree, 328
      fixtures, none stale or missing.
- [x] 2026-09-12 — Every family script runs standalone and sweeps only the kinds it
      produces. Seen red once: `thermo_functions.py` run alone removed `constants/R.json`
      until `Writer.finish` was limited to the kinds of the run.

      ⚠ 2026-09-27: this criterion is true only kind by kind, not script by script, for
      a kind several scripts write (`tp`, `hp`, `sp`, `rocket`): the parent `BOOT.md`'s ⚠
      of this date records the hazard, found running `propellants.py` alone for the
      sodium case above.
- [x] 2026-09-12 — The derived equilibrium cases reproduce their station: the tp, hp
      and sp solves at a station's values return the station's temperature (for the
      LOX/LH2 throat, 3292.3746 K against 3292.3746 K), so the package's hp and sp inputs
      are the enthalpy and entropy in J/kg divided by R (probe before the scripts were
      written; the `derivedFrom.temperature` field lets a test node re-check every case).

- [x] 2026-09-30 — `regenerate.py --check --sample` compares as documents with a
      tolerance and plain `--check` stays exact text (the parent's last criterion, which
      holds the evidence: the mutations, each applied alone and seen to fail or pass as
      designed, and the full regeneration whose 336 files moved only in `generatorSha256`
      and `generatedOn`). The CI run on `windows-latest` is still owed there; this
      criterion states the local half only.

      ⚠ 2026-09-30: the Invariants above still say "byte-identical fixtures", which holds
      for plain `--check`; the sampled check on another machine of the platform accepts
      the differences the parent's rows allow, since the same scripts, package and data
      files give bit-identical output only on the machine that wrote the files.

## Taboos

- No hand-edited fixture, no fixture value typed into a script.
- No import of the tree's code and no call into it: the generator must not depend on
  what it verifies.
- No tolerance decided here: the parent's `tolerances.json` is the only table
  (`document_comparison.py` reads its `regeneration` and `throat` rows and holds no
  figure of its own).
- No network access at generation time: the package and the data are local.
