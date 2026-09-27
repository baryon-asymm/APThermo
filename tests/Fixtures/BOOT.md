# BOOT.md — Fixtures

## Purpose

The reference outputs the tree is verified against, with their provenance, the
scripts that generate them, a loader for the test nodes, and the single tolerance
table used by every comparison with the reference. The reference implementation is
NASA CEA as published in the `cea` Python package (github.com/nasa/cea, Apache-2.0),
version 3.3.4, which ships prebuilt wheels for Windows and Python 3.11+ and the same
`thermo`/`trans` data. That repository publishes no output files, only inputs, so this
node generates the outputs itself, from committed scripts, and records how.

## Invariants

- **Every fixture carries its provenance**: package name and version, the library
  version string the package reports, the method (`cea-package`,
  `independent-evaluation`, or `cea-package-mass-flux-scan` for the throat family below,
  2026-09-27), the script name and its SHA-256, the SHA-256 of the
  package's `thermo.lib` and `trans.lib`, the SHA-256 of the tree's `data/thermo.inp`
  and `data/trans.inp`, and the generation date. The date is the day the content last
  changed: the writer leaves a fixture untouched when the regenerated document differs
  from the committed one only in the date, so regeneration is byte-identical.
- **Fixtures are generated, never edited.** A regenerated fixture that differs from
  the committed one is a finding about the data or the package version, recorded in
  this node's acceptance criteria, not a silent update.
- **One tolerance table**, in `tolerances.json`, with a derivation per entry; no test
  node keeps a tolerance for a comparison with the reference. The table also holds two
  entries that are no comparison with the reference but that two test nodes share and
  neither may read from the other (2026-09-14, decided at the root on the clean-code
  review's F-TF-05): `moleFractionFloor`, the mole fraction below which the GPU/CPU and
  union-batch comparisons assert nothing, and `polishThresholdRelative`, the second
  tier derived from the equilibrium solver's polish threshold, each with its derivation
  like every other entry. The rule that picks `moleFraction` or `moleFractionTrace` for
  a reference value is the table's too (`ToleranceTable.MoleFractionField`), so that
  the print threshold is written once, in the table (the review's F-AR-03 found it
  typed with its selection line in three test nodes).
- **The case matrix is explicit** (Constraints) and file names encode the case; a test
  enumerates a directory, it never lists cases by hand.
- **Units in fixtures are SI**, converted once in the generator from the package's
  units (bar, kJ/kg, kJ/(kg·K), kg/m³, m/s), and the conversion factors are named in
  the generator's header.
- **Multi-station rocket references are guarded.** The package's rocket solver can
  err silently after a melting plateau: it keeps a liquid below its range at later
  stations (−0.57 % of Ivac at `p_c/p` 100 on the AP/Al verification record), its
  sequential stations can drift off the chamber isentrope with no transition at all
  (−0.70 m/s at `p_c/p` 2000), and example 13 without its insert list loses 0.61 %
  of Ivac the same way — all three found 2026-09-13 against tp re-solves of the
  package itself. Every shifting station of a generated rocket reference is
  therefore checked before the fixture is written (a frozen station keeps the
  freezing station's composition by construction): no condensed species outside its
  joined record range unless its same-formula partner stands beside it (a pinned
  pair), and at every shifting station without such a pair a tp re-solve of the
  package at the station's (T, p) reproduces the station's entropy to 1e-6 relative
  — at a pinned station the tp state is degenerate and proves nothing. A station
  that fails is regenerated as a direct single-exit case from the chamber; a case
  that still fails is not committed. The guard lives in `generate/cea_cases.py`
  (`guard_stations`, run on every rocket solve) and prints one log line per
  solution.

## Dependencies

None.

Outside the tree: Python 3.11+, `cea` 3.3.4 and `numpy` 2.5.3 (pinned in
`generate/requirements.txt`; the environment lives in `generate/.venv`, ignored by
git); the tree's `data/` files; xunit is not used here (the loader is a plain library,
verified by [Fixtures.Tests](../Fixtures.Tests/BOOT.md)).

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Layout: `generate/` holds the Python scripts and `requirements.txt`; `cases/<kind>/`
  holds one JSON file per case (`kind` is `tp`, `hp`, `sp`, `rocket`, `throat`,
  `transport`, `thermo`, `constants`; `throat` since 2026-09-27); `tolerances.json` is the tolerance table; the C# loader is
  the node's assembly `APThermo.Fixtures`.
- Procedure: `python -m venv tests/Fixtures/generate/.venv`, install
  `requirements.txt` into it, then `python tests/Fixtures/generate/regenerate.py`
  (writes changed fixtures, removes stale ones) or `regenerate.py --check` (compares
  only, exit code 1 on any difference). Each script also runs standalone and sweeps
  only the kinds it produces.

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
  `throat`); regenerating a shared kind, or committing any change to a script that
  writes one, goes through the full `regenerate.py` driver, never a single family
  script alone.
- The node also owns the repository-path resolution every test node uses
  (`RepositoryPaths`): the root is the nearest directory above this node's source file
  that holds `AGENTS.md`, found with `[CallerFilePath]`, never by `../..` chains or
  from the binary's location (AGENTS.md §13).
- Fixture document: `{ "case": { "name", "kind", "inputs" }, "generator": { provenance },
  "outputs": { by name, SI } }`. Inputs carry the reactants (name or custom definition,
  mass fraction, temperature, and since 2026-09-27 `role`, `oxidizer` or `fuel`, in every
  case given with an oxidizer-to-fuel ratio, from the vectors the generator splits the
  kilogram with), `elementMoles` in kmol per kg computed by the generator
  from the file's formulas and the recorded mass fractions, `products` (the species
  list the package used), `omit` (the names given to the package, whether or not each
  names a product: the package ignores the rest), `only` when the case was given an
  explicit product list (RP-1311 examples 1 and 12), the problem values in SI, and
  for derived cases `derivedFrom`. Rocket outputs are
  `stations` in the package's order (chamber, throat, then the exits as given), each
  with `station`, `index`, `frozen`, the state fields named after `MixtureState`, the
  performance fields named after `PerformanceFigures`, the transport fields named after
  `TransportFigures` when transport is on, and `moleFractions` by species name, all
  species the reference reports, no threshold. Equilibrium outputs are one such state
  with `converged`. `dlnVdlnT`, `dlnVdlnP` and the sound speed of equilibrium cases are
  derived in `cea_cases.py` from the package's cp, cv, γ_s and M, with the relations
  named there; a frozen station carries the ideal-gas derivatives 1 and −1, and so does
  the chamber of a case frozen at the chamber, whose γ_s and sound speed the package
  reports as the frozen ones while its heat capacities stay the equilibrium ones.

  ⚠ 2026-09-12, found by the Problems tests: the package's `of_ratio_to_weights`
  holds the oxidizer-to-fuel ratio in single precision before it splits the kilogram
  (2.6 becomes 2.5999999046, 5.55157 becomes 5.5515699387), so the recorded mass
  fractions, and the `elementMoles` and reactant enthalpies computed from them, carry
  up to 6e-8 relative of that rounding against the nominal ratio; a ratio that is exact
  in single precision (LOX/LH2 at 4, 5, 6, 7, 8) carries none. The `oxidizerToFuelRatio`
  field records the nominal ratio. A tree that splits in double precision reproduces
  the recorded mass fractions to 1e-7 and, from the recorded mass fractions themselves,
  the element moles and enthalpies to rounding. Until then the inputs paragraph above
  said the element moles were computed "from the file's formulas" without naming the
  mass fractions they multiply, and did not mention `only`.

  ⚠ 2026-09-12, three caveats of the reference's fields, found by the Equilibrium tests
  and confirmed with probes of the package on the reference machine:
  - `mixtureMolarMass` (the package's `MW`) is one kilogram over the moles of all species
    with the condensed ones counted as moles; `molarMass` (its `M`) is one kilogram over
    the gaseous moles. The field was named `gasMolarMass` until then.
  - `cvFrozen` and `cvEquilibrium` at a frozen station are not computed by the reference:
    the exits carry 0 and a frozen throat carries the chamber's values. Test nodes do not
    compare them at frozen stations.
  - With transport on, the package's `cp_fr` and `cv_fr` of a mixture that holds
    condensed species are those of the gas phase per kilogram of gas (AP/HTPB/Al chamber:
    2038.5 with transport, 1904.5 kJ/(kg·K)·10⁻³ without, the latter being the sum over
    all species), while cp_eq, γ_s, M and MW do not change. Hence the derived
    equilibrium cases are generated without transport (below), and the `cpFrozen` and
    `cvFrozen` of a rocket station with condensed species and transport on are gas-phase
    values, to be compared as such by the performance tests node.

    ⚠ 2026-09-12, made precise by the source of the package (`source/equilibrium.f90`,
    `compute_transport_properties`) when the Transport node was written: with transport
    on, `cp_fr` at every station is the frozen heat capacity of the package's transport
    set (at most 40 gaseous species chosen as the Transport `BOOT.md` describes) per
    kilogram of that gas, and `cv_fr` is that value minus n R with n the gaseous moles of
    the whole mixture. Without condensed species the set covers the gas to 1e-6 and the
    value is the whole mixture's within the tolerance; the Transport tests compare the
    field with the set's heat capacity at every station with transport.

  ⚠ 2026-09-12, two more caveats found by the Transport node:
  - The package estimates the transport properties of a gaseous species without an
    entry in `trans.inp` (hard spheres for the viscosity, the modified Eucken relation
    for the conductivity, as CEA2 did); it excludes nothing. The tolerance derivations
    of the transport fields said "excluded on both sides"; reworded.
  - The package's reacting conductivity is defective at a station where one of the
    component species that seed its transport set is a trace (x < 1e-10 of the set):
    the Fortran rewrite writes `continue`, a no-op, where CEA2 had `GOTO 260` in the
    elimination of the trace species, keeps the reaction through it while dropping its
    pairs, and reports a reacting conductivity 170 to 440 times the frozen one. Nine
    stations of the committed fixtures carry it: the exits of LOX/LH2 O/F 4 at 5, 7
    and 10 MPa (both exits, 645 to 1020 K) and the second exit of LOX/LH2 O/F 5 at 5, 7
    and 10 MPa (910 to 916 K), where `OH` seeds the oxygen row. The fixtures are not
    edited (first invariant); the Transport tests skip `reactingConductivity` and
    `reactingPrandtl` where the tree's solver reports a trace elimination, assert the
    defect is still visible there, and fail when it is gone, so that a regenerated
    reference removes this caveat rather than hiding it. `reactingPrandtl` at those
    stations is deflated (0.34–0.41 against 0.53–0.61) by the same inflation.

  ⚠ 2026-09-13, found by the melting-plateau cases: at a tp assigned exactly at a
  bound two records of one substance share, the package's derivative matrix is
  singular (the stop its manual documents as "derivative matrix singular") and it
  prints zero equilibrium heat capacities with `γ_s = −1/dlnVdlnP` instead of the
  chosen record's derivatives; the composition itself converges and matches the
  tree's (`rp1311-example13-mixture_T2373`; at 2851 K the same mixture picks a side
  cleanly and carries real derivatives). The fixtures are not edited (first
  invariant); the equilibrium comparisons of the Equilibrium and Problems tests skip
  the second-order fields — `cpEquilibrium`, `cvEquilibrium`, `gammaS`, `dlnVdlnT`,
  `dlnVdlnP`, `soundSpeed` — exactly where a tp fixture prints `cpEquilibrium` 0, a
  value no real tp state has: the reference's own output is the signature, so a
  regenerated reference without the defect resumes the full comparison by itself.
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
    products expected (`AL2O3(L)` in the chamber).
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
  - `throat` (2026-09-27): the chamber and the throat of a shifting-equilibrium
    rocket, with no exit, where the throat is the largest mass flux `ρu` along the
    chamber isentrope, found over the package's own sp solves and never taken from its
    rocket solver. It exists because the package's rocket solver reports a wrong
    throat at the high-pressure edge of a melting plateau, while its equilibrium
    solves there are sound. The performance node's `BOOT.md` states the defect, and
    RP-1311 sections 6.3.3 and 6.3.4 define the throat this family computes.
    - Cases, the enthalpy assigned relative to the reactants' own `h₀`:
      - AP/HTPB/Al of the plateau cases above at 7 MPa, `h₀` − 2.20, − 2.225,
        − 2.25, − 2.275 and − 2.30 MJ/kg;
      - the same at 1, 3 and 15 MPa, `h₀` − 2.25 MJ/kg;
      - RP-1311 example 13's propellant at 5 MPa, `h₀` and `h₀` + 250 kJ/kg, with
        its trace threshold.
    - Method, per case:
      1. The chamber is the package's hp at the assigned enthalpy and `p_c`.
      2. `ρu` is evaluated on 101 pressure ratios `p/p_c` evenly from 0.45 to 0.70,
         each an sp solve at the chamber's entropy with `u = √(2(h_c − h))`. The
         largest must lie strictly inside the grid, or the run stops.
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
    - The document is that of the rocket kind, with `stations` holding the chamber
      and the throat only, and `inputs` marking `enthalpyAssigned` as the hp band
      cases do. Frozen flow is not generated: a frozen throat has no plateau.
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
- Tolerance table, provisional until the first full comparison calibrates it
  (derived from the reference's print precision in its own sample output and its
  convergence criteria; every entry is confirmed or reworded in the acceptance criteria):

  | Field | Absolute | Relative |
  |---|---|---|
  | temperature | 0.05 K | 2e-5 |
  | pressure | — | 1e-4 (calibrated from 1e-5, see the criteria) |
  | density, enthalpy, entropy, molar mass, heat capacities, `γ_s`, sound speed | — | 1e-4 |
  | mole fractions | 5e-6 | — (species below 5e-6 in the reference: only "below 1e-5" is asserted) |
  | `c*`, `Isp`, `Ivac`, `C_F`, area and pressure ratios | — | 1e-4 |
  | transport properties, Prandtl numbers | — | 5e-4 |
  | `thermo` function fixtures | — | 1e-12 |

## Shape exceptions

Added 2026-09-14 by the design session, after the protocol tests node's measurements found
this constructor over the root's six parameters. `Provenance` mirrors, field for field, the
`generator` block every fixture file carries. On the root's condition for such a type its
one creation names its arguments (the criterion of 2026-09-15 below).

⚠ 2026-09-15: this paragraph read "it passes them by position today (the criterion
below)", true when it was written but not of the code: the constructor's one call site
already named every argument then, through an index into a side array kept in step with
the parameter order by hand (`ProvenanceStrings`), which the root's named-argument
condition does not by itself rule out but which is exactly the swap hazard the condition
exists to guard against. Re-cut by the repair review of 2026-09-15
(`CeaFixtures.ReadProvenance`): each argument now reads its own named field of the
`generator` block directly, with no side array to keep in step.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Provenance.Provenance` | parameters | 11 | the `generator` block of a fixture file, field for field; its one creation names its arguments |

## Acceptance criteria

- [x] 2026-09-12 — Every fixture regenerates byte-identically from the committed
      scripts with the pinned package on the reference machine:
      `regenerate.py --check` exits 0 on the committed `cases/` directory after a full
      regeneration, and exits 1 with the file named when a fixture is altered, missing
      or stale (run by hand on the reference machine).
- [x] 2026-09-12 — The loader reads every fixture and the tolerance table, and a
      fixture missing a field of its kind fails the loader with the file name and the
      field: `Fixtures.Tests` (`FixtureLoadingTests`, `ToleranceTableTests`,
      `MalformedFixtureTests`).
- [ ] The HTPB definition is decided and cited. Proposal: formula
      C 7.3165 H 10.3416 O 0.0674, enthalpy −250 cal/mol (−1046.0 J/mol) at 298.15 K,
      the definition used by common CEA front ends; to be confirmed against a cited
      source. Used provisionally by `propellants.py`, with the note recorded in the
      fixture inputs (`reactants[].note`), so the AP/HTPB/Al fixtures change when the
      definition does.
- [x] 2026-09-12 — `data/thermo.inp` and `data/trans.inp` are committed verbatim from
      the nasa/cea tag `v3.3.4` (commit `4c5c612efa2002a94e3a5a1f33b1674d55c65340`), the
      release that produced the 3.3.4 wheel; the tag, the commit and the SHA-256 of both
      files are recorded in `data/NOTICE`.
- [x] 2026-09-12 — The tree's species selection for each case equals the package's
      product list recorded in the fixture inputs (`products`); checked where the tree
      selects species, not in the generator, which cannot run the tree:
      `Problems.Tests.PropellantTests.CandidateSpeciesEqualTheReferenceProductList`
      over every rocket, tp, hp and sp file (the list from the directory listing, 195
      that day). The RP-1311 examples 1 and 12 were generated with an explicit product
      list, which the package takes as given; since 2026-09-12 the fixture inputs
      record it (`only`, see the inputs paragraph), and the comparison uses it.

      ⚠ 2026-09-12: stood "the generator asserts that the package's species list
      equals the one `Data` reads": a Python script cannot call the tree; the fixture
      records the list and the comparison moves to the node that selects species.
- [x] 2026-09-13 — The melting-plateau cases are generated with the station guard in
      force: the full regeneration re-solved 98 rocket solutions through the guard
      with a worst entropy residual of 8.3e-8 and no range failure, the plateau
      fixtures carry the convention values of the matrix above, and example 13
      regenerates byte-identically with its insert list recorded (the regeneration
      runs of 2026-09-13: `regenerate.py` over every kind, unchanged everywhere but
      the new and reprovenanced files; `Fixtures.Tests` green on form and
      provenance).
- [x] 2026-09-12 — Tolerances calibrated after the first full comparison; every entry
      confirmed or reworded with the reason, with the date. 2026-09-12: the tp, hp and sp kinds (106
      files) passed the table unchanged in the Equilibrium tests, the frozen stations of
      the rocket kind (51 files) in their frozen-mode test. 2026-09-12: the rocket kind
      (89 files) passed in the Performance tests after one calibration: `pressure` from
      1e-5 to 1e-4 relative, because the reference's throat and area-ratio pressures carry
      its iteration residual of up to 4e-5 (RP-1311 equations 6.16 and 6.25; 3.9e-5
      observed), while an assigned pressure stays exact. 2026-09-12: the transport kind
      (27 fit files, `transportFit` 1e-12) and the transport fields of the rocket kind
      (39 files with transport) passed the table unchanged in the Transport tests, the
      stations evaluated on the reference composition: worst 3.4e-8 relative against the
      5e-4 of the table, the rest of the budget being for the comparison on the tree's
      own composition, which the Problems tests node makes. 2026-09-12: the end-to-end
      comparison in the Problems tests node passed the table unchanged: every rocket
      file on the tree's own composition with its transport fields
      (`RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`), every tp, hp
      and sp file singly and as state records in batches over unions of elements
      (`EquilibriumTests`).
- [x] 2026-09-14 — The shared rules of 2026-09-14: `ToleranceTable.MoleFractionField`
      picks the entry by the table's own `moleFraction` threshold, and the equilibrium
      (`Equilibrium.Tests/StateComparison.cs`), performance
      (`Performance.Tests/StationComparison.cs`) and front door
      (`Problems.Tests/ReferenceComparison.cs`) tests nodes call it instead of a
      constant and a selection line of their own; `moleFractionFloor` and
      `polishThresholdRelative` stand in the table with their derivations, and the
      execution (`Execution.Tests/GpuCpuTolerances.cs`) and front door
      (`Problems.Tests/RocketTests.cs`) tests nodes read them instead of their copies;
      `Fixtures.Tests.ToleranceTableTests.MoleFractionFieldPicksByTheThresholdAndOneUlpOnEachSide`
      proves the rule at the threshold and one ULP on each side, seen red once with the
      comparison reversed (`<` for `>=`), reverted before that test was committed, the
      evidence recorded in the test's own doc comment.
- [x] 2026-09-15 — The creation of `Provenance` in `CeaFixtures` names its arguments,
      each bound to the `generator` field its value is read from (the root's condition
      on a declared wide constructor, the row of `## Shape exceptions`): one site,
      `CeaFixtures.ReadProvenance`, fully named; covered by
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, green at
      `62cd99e`; every fixture theory of the tests nodes
      green unchanged: the full-solution fast suite (10 projects, 3014 tests) green,
      every consumer's `Bits.approved.txt` hash unmoved.

      ⚠ 2026-09-15: this tick first cited `CeaFixtures.cs:92`, the one call site as it
      stood that day, each argument bound to an index of a side array
      (`ProvenanceStrings`) kept in step with `Provenance`'s parameter order by hand —
      already named, so this criterion's own check passed, but the swap hazard the
      named-argument condition exists to guard against was still there one level up, in
      the array. Re-cut the same day by the repair review (R-Fixtures-1) into
      `ReadProvenance`, each argument now reading its own named field directly; the
      re-verification is this tick's own evidence, not a new one.


- [x] 2026-09-27 — The throat family (the case matrix): `tests/Fixtures/generate/throat_scan.py`
      (merged as `efe7d7e`), registered in `regenerate.py`.
      - `regenerate.py` writes the ten cases, and `regenerate.py --check` exits 0 right
        after (over all 327 fixtures of every kind).
      - The method guard (cea's own rocket throat, wherever it is sonic within 1e-4 of
        Mach 1, must match the scan's c* within 1e-5 relative) was shown red once by
        cutting the grid to exclude the true peak (`the largest mass flux lies at the
        grid edge`, on the sonic dh −2.20 MJ/kg AP/HTPB/Al case at 7 MPa), and again by
        perturbing the scan's returned c* by 1 % on the same case (`scan c* … against
        the package's sonic throat c* …`); both reverted before this tick. The other
        named mutation, the bracket's high-pressure end replaced by the low one, was
        tried first and found degenerate on a regular (non-plateau) case: the two ends
        agree to rounding there (`BOOT.md`'s own Method step 4), so the swap moves
        nothing and cannot serve as red-once evidence; the grid-edge and the
        c*-perturbation mutations replace it.
      - The loader reads the kind (`FixtureLoadingTests.TheKindsPresentAreThoseOfTheCaseMatrix`,
        `EveryFixtureNamesTheScriptThatWroteIt` now knows `cea-package-mass-flux-scan`)
        and `ToleranceTableTests.EveryStateFieldOfTheFixturesHasATolerance` covers its
        form (`[InlineData("throat")]`, alongside `rocket`, whose `stations` array shape
        it shares); `tests/Fixtures.Tests` 28/28 green.
      - The logged c* values reproduce the measurements above to 0.001 m/s: 1336.5371,
        1333.0661, 1330.4435, 1330.4352, 1333.2261 m/s at 7 MPa; 1957.7526 and
        1941.0062 m/s for example 13 (against 1336.537, 1333.066, 1330.444, 1330.435,
        1333.226, 1957.753 and 1941.006 m/s recorded above).

      ⚠ 2026-09-27, found while writing the guard: `cea_cases.solve_rocket` raises
      *before* Mach or c* can be read from its `RocketSolution`, because its own
      station guard (`guard_stations`, the multi-station entropy-consistency check)
      runs right after the solve and is exactly what catches the chamber/throat
      inconsistency at a plateau edge — the defect this family exists to work around.
      The design's wording ("the fixture records the package's throat under
      `outputs.packageRocketThroat` (c*, Mach, pressure ratio)") assumed the package's
      Mach would always be readable even when far from 1; empirically it is not, for
      exactly the cases that need the fallback. `packageRocketThroat` therefore holds
      `{"cStar", "mach", "pressureRatio"}` when the package's own solve and guard both
      pass, and `{"guardError": "…"}` (the guard's message) when they do not; no test
      reads either shape.

      ⚠ 2026-09-27: `throat_scan.py` reuses `plateaus.py`'s AP/HTPB/Al composition and
      `rp1311.py`'s example 13 mixture by import, as this node's Constraints require.
      `rp1311.py`'s `example13()` built its reactants, temperatures, O/F ratio, insert
      list and trace threshold as local variables; they are now module-level constants
      (`EXAMPLE13_REACTANTS`, `EXAMPLE13_TEMPERATURES`, `EXAMPLE13_OF_RATIO`,
      `EXAMPLE13_INSERT`, `EXAMPLE13_TRACE`) plus an `example13_mixture()` builder, with
      no change to any computed value. `cea_cases.solve_rocket` gained an optional
      `enthalpy` override (the scan's assigned enthalpy, not the reactants' own) and
      `rocket_inputs` an optional `extra` dict, mirroring `equilibrium_inputs`, for the
      `enthalpyAssigned` marker the hp band cases already carry. Touching `rp1311.py`'s
      bytes re-provenanced (new `scriptSha256`, `generatedOn`) all 63 of its
      already-committed fixtures; verified field by field (every key but `generator`)
      that none of them differs from the committed ones. This is the ordinary
      consequence of the first invariant above ("fixtures are generated, never
      edited") applied to a shared generator module, not a hand edit, and is recorded
      here rather than left to be found in the diff.

- [x] 2026-09-27 — The sodium case (the case matrix): `propellants.py` gains
      `sodium_hp`, one hp case (`cases/hp/nano3-rp1_of4_pc7MPa.json`) of NaNO3(a) with
      RP-1, both at 298.15 K, O/F 4, 7 MPa, generated the way `plateaus.py`'s
      `fuel_rich_hp` generates a standalone hp case (`make_mixtures`,
      `describe_reactants`, `solve_equilibrium`, `equilibrium_inputs`), transport off as
      every equilibrium-only case is.
      - The package converges (`converged: true`) to 1741.58 K; its candidate product
        list carries `NaCN(II)`, the six-interval record `Thermo`'s table limit refused
        until this date (its `BOOT.md`), at mole fraction 0.0 in the solution — the case
        exists for the candidate list, not for a nonzero `NaCN(II)` composition.
      - `regenerate.py --check` exits 0 over all 328 fixtures after `regenerate.py`
        writes the one new file.
      - Touching `propellants.py` re-provenanced (new `scriptSha256`, `generatedOn`) all
        135 of its already-committed fixtures; verified the same way as the `rp1311.py`
        entry above (`git diff --numstat`: every one of the 135 changes exactly 2 lines,
        and grep over the diff's added and removed lines found none outside
        `scriptSha256` and `generatedOn`). The ordinary consequence of the first
        invariant, as above, not a hand edit.
- [x] 2026-09-27 — Every reactant of a case given with an oxidizer-to-fuel ratio records
      its `role`, written by the generator from the oxidizer and fuel vectors it passes
      to the package. `regenerate.py --check` exits 0 after the regeneration, and
      `Fixtures.Tests` refuses a ratio case whose reactant lacks a role, red once on a
      copy.

      ⚠ 2026-09-27: the role lived nowhere in the fixtures. The front door's tests
      guessed it from a hand-typed set of five oxidizer names, which the sodium case's
      `NaNO3(a)` was missing from: both reactants read as fuel, and four facts threw
      "an oxidizer-to-fuel ratio needs at least one oxidizer and one fuel". Found by
      the coder who added the case.

      Evidence: `cea_cases.describe_reactants` gains `oxidizer` and `fuel` parameters
      (the same vectors the caller already built for `of_ratio_to_weights`) and writes
      each reactant's `role` (`"oxidizer"` where the oxidizer vector is positive,
      `"fuel"` where the fuel vector is positive, nothing when the case carries no
      ratio) — never a name list. Every ratio call site of `propellants.py`, `rp1311.py`
      and `plateaus.py` passes its own vectors through; `throat_scan.py`'s
      `example13_throats` imports `rp1311.py`'s newly hoisted `EXAMPLE13_OXIDIZER`/
      `EXAMPLE13_FUEL` constants for the same reason `example13_mixture` was already
      shared, never by copy.
      - `regenerate.py` (full driver only, the standalone-run hazard above) writes 224
        fixtures; `regenerate.py --check` exits 0 over all 328 immediately after.
      - A structural, field-by-field comparison of every changed fixture against its
        previous committed content (every key but `role`, `generator.scriptSha256` and
        `generator.generatedOn`) found zero mismatches over the 224 files: 175 gained a
        `role` on each reactant, the rest were re-provenanced only, by touching the
        shared scripts (`propellants.py`, `rp1311.py`, `plateaus.py`, `throat_scan.py`).
      - `CeaFixtures.Load` refuses a reactant of a ratio case with no `role`
        (`RequireReactantRoles`, naming the file and the reactant's index);
        `MalformedFixtureTests.ARatioCaseReactantWithNoRoleIsRejected` proves it on a
        copy in a temporary directory, shown red once by relaxing the guard so it never
        ran (reverted before this tick); `ARoleIsNotRequiredWithoutARatio` proves a
        role is not demanded where there is no ratio. `dotnet test tests/Fixtures.Tests`:
        30/30, `FixtureLoadingTests` confirming every committed fixture still loads.

## Taboos

- No hand-edited fixture, no fixture from an unpinned package version.
- No tolerance decided in a test node for a comparison with the reference.
- No fixture without provenance fields.
- No fixture whose inputs differ from the case matrix without the matrix being updated first.
