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
  version string the package reports, the method (`cea-package` or
  `independent-evaluation`), the script name and its SHA-256, the SHA-256 of the
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
  holds one JSON file per case (`kind` is `tp`, `hp`, `sp`, `rocket`, `transport`,
  `thermo`, `constants`); `tolerances.json` is the tolerance table; the C# loader is
  the node's assembly `AerospacePropellantThermodynamics.Fixtures`.
- Procedure: `python -m venv tests/Fixtures/generate/.venv`, install
  `requirements.txt` into it, then `python tests/Fixtures/generate/regenerate.py`
  (writes changed fixtures, removes stale ones) or `regenerate.py --check` (compares
  only, exit code 1 on any difference). Each script also runs standalone and sweeps
  only the kinds it produces.
- The node also owns the repository-path resolution every test node uses
  (`RepositoryPaths`): the root is the nearest directory above this node's source file
  that holds `AGENTS.md`, found with `[CallerFilePath]`, never by `../..` chains or
  from the binary's location (AGENTS.md §13).
- Fixture document: `{ "case": { "name", "kind", "inputs" }, "generator": { provenance },
  "outputs": { by name, SI } }`. Inputs carry the reactants (name or custom definition,
  mass fraction, temperature), `elementMoles` in kmol per kg computed by the generator
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
one creation names its arguments; it passes them by position today (the criterion below).

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
      `Problems.Tests.PropellantTests.Candidate_species_equal_the_reference_product_list`
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
      (`RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`), every tp, hp
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
      `Fixtures.Tests.ToleranceTableTests.MoleFractionField_picks_by_the_threshold_and_one_ulp_on_each_side`
      proves the rule at the threshold and one ULP on each side, seen red once with the
      comparison reversed (`<` for `>=`), reverted before that test was committed, the
      evidence recorded in the test's own doc comment.
- [ ] The creation of `Provenance` in `CeaFixtures` names its arguments, each bound to the
      `generator` field its value is read from (the root's condition on a declared wide
      constructor, the row of `## Shape exceptions`); every fixture theory of the tests
      nodes green unchanged.

## Taboos

- No hand-edited fixture, no fixture from an unpinned package version.
- No tolerance decided in a test node for a comparison with the reference.
- No fixture without provenance fields.
- No fixture whose inputs differ from the case matrix without the matrix being updated first.
