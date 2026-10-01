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
  version string the package reports, since 2026-09-27 the SHA-256 of the whole
  generator (`generatorSha256`, defined in `generate/BOOT.md`), the method (`cea-package`,
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

  ⚠ 2026-09-30: the sentence stood as "holds two entries that are no comparison with the
  reference". It holds two more since the binding step compares as documents
  (`generate/document_comparison.py`): `regeneration`, the relative tolerance of the
  generator's own output regenerated on another machine of the platform that wrote the
  committed files (1e-9), and `throat`, the wider one for the numbers the throat family's
  mass-flux search derives (5e-5). Neither is a comparison with the reference nor between
  two paths of the tree; each carries its derivation like every other entry, and the
  generator reads both from this table, so no tolerance is decided in the script. The
  two of 2026-09-14 are shared by test nodes; these two are read by the generator.
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
  `transport`, `thermo`, `constants`, `reactant`; `throat` since 2026-09-27, `reactant` since
  2026-09-28); `tolerances.json` is the tolerance table; the C# loader is
  the node's assembly `APThermo.Fixtures`.

  ⚠ 2026-09-28: the `reactant` kind was added for a reactant-level fact that cannot be an
  `hp`/`tp`/`sp`/`rocket` case: `Br2(cr)` (`propellants.py`'s `br2_reactant_anomaly`), whose
  one interval is written 300 -> 265.9 K (the second hidden-defect audit's observation 6,
  `Problems` BOOT.md). The package's own equilibrium and rocket solvers do not converge a
  pure-`Br2(cr)` problem near its own reactant enthalpy ("Mixture temperature outside of
  allowable bounds", checked by hand against the pinned package), consistent with the
  record being out of range for the candidacy test too (`src/Thermo/HISTORY.md#record-bounds`: "Br2(cr)
  is in range nowhere, in the reference too"). A `reactant` case carries no equilibrium
  solve: `inputs.reactants` (the reactant description, as every other kind's `inputs` does)
  and `inputs.temperature`, `outputs.enthalpyPerKilogram` from the package's own
  `Mixture.calc_property`, the same call every other kind's `reactantEnthalpy`/`enthalpy`
  comes from. `tests/Fixtures.Tests/FixtureLoadingTests.cs`'s kind matrix carries the same
  addition, since it reads the kind list from a directory listing but pins the expected set
  by name (AGENTS.md §13, "a criterion with the quantifier 'all' is checked against a list
  generated by the machine").

  ⚠ 2026-09-28: this paragraph first claimed that no other file of the case matrix moved
  (a scoped `python regenerate.py reactant`, never the unscoped driver) and that a full,
  unscoped run would move about 318 existing cases by roughly 1e-7 relative, last-ULP
  drift of the kind the root's platform constraint already documents for the tree's own
  bits. That was never checked field by field; it was inferred from `regenerate.py
  --check` reporting those 318 cases "changed" and read as a value difference. It is not:
  touching `propellants.py`'s bytes re-provenances every fixture's `generatorSha256`
  (spanning the whole `generate/` directory, `generate/BOOT.md`'s rule) and, for the 136
  `propellants.py` produces itself, its own `scriptSha256`, the ordinary consequence the
  provenance criterion below already describes for `rp1311.py` and for `propellants.py`'s
  own `sodium_hp` addition. `python regenerate.py` (the full driver) written after this
  addition moved exactly those 318 fixtures, each a 2- or 3-line diff
  (`generatorSha256`, `scriptSha256` where the fixture is `propellants.py`'s own, and
  `generatedOn`); `git diff -- tests/Fixtures/cases | grep -E "^[+-]"` outside `+++`/`---`
  matched no line other than those three keys, over all 318 files, and `regenerate.py
  --check` exits with `fixtures: unchanged 319` immediately after. No case's output value
  moved.
- Procedure: `python -m venv tests/Fixtures/generate/.venv`, install
  `requirements.txt` into it, then `python tests/Fixtures/generate/regenerate.py`
  (writes changed fixtures, removes stale ones) or `regenerate.py --check` (compares
  only, as exact text, exit code 1 on any difference). `regenerate.py --check --sample`
  compares a sample as documents, field by field, with a tolerance (the last criterion
  below, `generate/API.md`). Each script also runs standalone and sweeps
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
  `throat`, `reactant`); regenerating a shared kind, or committing any change to a script that
  writes one, goes through the full `regenerate.py` driver, never a single family
  script alone. `reactant` is written only by `propellants.py`, but that script also writes
  the shared `tp`/`hp`/`sp`/`rocket` kinds, so it is regenerated through `regenerate.py`
  scoped to the one kind it owns alone (`python regenerate.py reactant`) rather than
  standalone, when only that kind needs a new case.
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
  for derived cases `derivedFrom`. This is the shape of `tp`, `hp`, `sp` and `rocket`
  documents, every one an equilibrium or a rocket solve; `reactant` documents (2026-09-28)
  are narrower, one reactant with no equilibrium behind it: `inputs.reactants` (one entry,
  the same reactant description) and `inputs.temperature`, `outputs.enthalpyPerKilogram`
  alone. Rocket outputs are
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
| `Provenance.Provenance` | parameters | 12 | the `generator` block of a fixture file, field for field; its one creation names its arguments |

⚠ 2026-09-27: the row read 11. The guards audit's F7 added `generatorSha256` to the `generator` block
(`generate/BOOT.md`'s rule), so `Provenance` gained a field of its own; the row now records the current
measurement, still one call site (`CeaFixtures.ReadProvenance`), still fully named.

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

- [x] 2026-09-28 — The throat family's four F1 cases (the case matrix; the second
      hidden-defect audit's finding F1, `src/Performance/HISTORY.md#throat-first-maximum`): `throat_scan.py` gains
      `_first_local_max` (the first local maximum met scanning from the chamber side,
      in place of the grid's overall maximum), the `ELEMENT_MIXTURE_CASES` list and
      `element_mixture_throats`, and the guard's second accepted branch (the ⚠ of
      2026-09-27 above, extended: the package's own rocket solver can converge to the
      same wrong, second/downstream sonic point, so its throat is sonic but not equal
      to the scan's; accepted when the scan's `p/p_c` is upstream of the package's and
      its c* is no higher).
      - `regenerate.py throat` writes all 14 cases of the family (the ten of
        2026-09-27 plus the AP/HTPB/Al `h₀` − 2.625 MJ/kg case and the lean Al/O/H,
        B2O3 and LiF element-mixture cases), and `regenerate.py --check throat` then
        reports `fixtures: unchanged 14`.
      - Touching `throat_scan.py` re-provenanced (new `scriptSha256`) the ten
        already-committed throat fixtures, and its `generatorSha256` (the hash over
        every `*.py` and `requirements.txt` of `generate/`, this node's own rule
        above) re-provenanced every other fixture of the tree as well — 312 of them,
        `regenerate.py` run in full, not by kind. Verified field by field, over all
        322 changed files, that only `generator.*` keys differ from the committed
        ones; `Fixtures.Tests` 34/34 green after, `EveryFixturesGeneratorSha256MatchesTheCommittedGenerator`
        included.
      - The four new cases' own figures (scan c*, its `p/p_c`, and the package's own
        downstream sonic throat where the guard's second branch accepts the case) are
        recorded in the performance node's `BOOT.md`, matched to the audit report's own
        cited numbers for the AP/HTPB/Al case.

      ⚠ 2026-09-28: the "ten cases" and "all 327/328 fixtures" figures of the entries
      above are the state of 2026-09-27 and are left as written (AGENTS.md §8: a
      number that repeats a list's length is not corrected retroactively once the
      list has changed; it was true the day it was ticked). The throat family now
      holds 14 cases; the repository-wide fixture total is not re-quoted here, since
      no criterion of this node asserts it as an "all" quantifier the machine checks
      — `regenerate.py --check` (no kind filter) is that check, and it is run by the
      full suite (`Fixtures.Tests`), not read off a typed number in this document.

- [x] 2026-09-28 — The throat family's three F3 cases (the case matrix; the second
      hidden-defect audit's finding F3, `src/Performance/HISTORY.md#crit-second-audit`): `throat_scan.py` gains
      the `PLATEAU_EDGE_CASES` list (representative points of the audit's own Li2O
      (`li-o-h`) and BeO/H2O (`be-o-h`) systems) and `plateau_edge_throats`, sharing
      `element_mixture_throats`'s generation method (renamed `_element_mixture_throats`,
      taking the case list as a parameter) rather than a second copy of it.
      - `_scan_throat`'s ternary refinement is made robust to a trial that does not
        converge (`_flux_or_negative_infinity`, and one retry on the converged
        bracket's own edge): the audit's own 0.3 MPa Li2O enthalpy band has a
        razor-thin non-convergent ratio (measured at 0.58239774, width under 1e-7)
        arbitrarily close to the search's own converged bracket, on every one of its
        14 h-values tried; without the guard, `regenerate.py throat` raised
        `RuntimeError: sp solve did not converge (last_error 8)` on all 14. A
        non-convergent trial is treated as strictly worse than any converged one, so
        the search steps past it rather than raising; the converged bracket's own
        edge is unaffected (measured: `li2o-throat_pc0.3MPa_h3.29375MJkg`'s scan c*
        and `p/p_c` match a direct evaluation at the same ratio without the guard).
      - `regenerate.py throat` writes all 17 cases of the family (14 of 2026-09-28
        plus the three new ones), and `regenerate.py --check throat` then reports
        `fixtures: unchanged 17`.
      - Two of the three (`li2o-throat_pc0.3MPa_h3.29375MJkg`,
        `li2o-throat_pc3MPa_h2.2375MJkg`) reproduce the reference within the family's
        usual tolerance. The third, `beo-h2o-throat_pc15MPa_h-11.06875MJkg`, does not:
        its own reference (the scan's ternary refinement) lands on the pinned-pair
        side of the plateau edge itself (Mach 1.011285688885813), the same defect F3
        fixes in the tree, now found in the reference instead. The Performance node's
        own `StationComparison.IsPlateauEdgeDivergence` skips that one station's
        comparison, guarded on the reference's own recorded Mach (never a blanket
        exemption); `tests/Performance.Tests/BOOT.md`'s 2026-09-28 entry has the
        red-once evidence. This node's own acceptance below is unaffected: the
        fixture's `chamber` station (single-phase throughout) still reproduces the
        reference, and its provenance and form are checked exactly like any other
        fixture's.
      - The audit's own two named 3 MPa points (h 2.20625 and 2.2125 MJ/kg) are not
        the ones committed: at both, the tree's fixed throat search ends
        `ThroatNotFound` (a real, separate outcome, not investigated further here —
        Performance `BOOT.md` owns that code), so a nearby point of the same band
        (h 2.2375 MJ/kg) that ends `Ok` was used instead, still within the audit's own
        band and citing the audit's system and pressure.

      Evidence: `dotnet test tests/Performance.Tests` 1418/1418 (`src/Performance/HISTORY.md#crit-second-audit`
      has the F3 fact's own red-once record); `Fixtures.Tests` 34/34;
      `regenerate.py --check` (no kind filter), after the full-tree re-provenance
      below, exits 0.

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

- [x] 2026-09-27 — The provenance ties every fixture to the committed generator (the
      guards audit of 2026-09-26, F7): every fixture's `generator` block now carries
      `generatorSha256` (`generate/BOOT.md`'s rule: the SHA-256 of the concatenation,
      over every `*.py` file of `generate/` and `requirements.txt` in ordinal order of
      their names, of the file name, a LF and the file's bytes with CRLF normalized to
      LF), and `scriptSha256` uses the same CRLF-to-LF normalization as `generatorSha256`,
      so a Windows and a Linux checkout agree on both.
      - `regenerate.py` (full driver only) wrote 318 of the 328 fixtures; `regenerate.py
        --check` exits 0 immediately afterward, none stale or missing.
      - A structural, field-by-field comparison of every changed fixture's JSON against
        its previously committed content, with `generatorSha256`, `scriptSha256` and
        `generatedOn` stripped from both sides before comparing, found zero mismatches
        over the 318 files: the regeneration touched provenance only.
      - `Fixtures.Tests` gains `EveryFixturesScriptSha256MatchesItsCommittedScript`,
        `EveryFixturesGeneratorSha256MatchesTheCommittedGenerator` and
        `AllFixturesCarryOneThermoLibSha256AndOneTransLibSha256`
        (`FixtureLoadingTests.cs`), each failing on an empty fixture set (`Assert.NotEmpty`
        or the loop's own `AllCases()`, which throws first). `dotnet test
        tests/Fixtures.Tests`: 33/33.
      - Each shown red once, reverted before this tick:
        - a temporary fact loaded a copy of `cases/tp/rp1311-example14_T300.json` from a
          temporary directory with `scriptSha256` replaced by 64 zeros and re-ran the
          script-hash comparison against it: `"...\rp1311-example14_T300.json:
          scriptSha256 does not match the committed rp1311.py"`; the temporary fact was
          deleted afterward;
        - a comment added to `common.py`, left unregenerated, turned
          `EveryFixturesGeneratorSha256MatchesTheCommittedGenerator` red:
          `"...\cases\constants\R.json: generatorSha256 does not match the committed
          generator"`; the comment was reverted and the fixtures were confirmed
          unchanged (`git status`).
      - `Provenance` gained a twelfth parameter (`GeneratorSha256`), read by
        `CeaFixtures.ReadProvenance` like every other field, named at its one call site;
        the `## Shape exceptions` row above is re-measured (12).
      - No `Bits*.approved.txt` or `Throughput*.approved.txt` moved. `PublicSurface.approved.txt`
        did move, in the same commit: `APThermo.Fixtures` carries no reference to xunit, so
        `Protocol.Tests.SurfaceTests` reads it as a library assembly and `Provenance`'s
        added parameter and property are on the snapshot (its `.ctor` line and one new
        `String GeneratorSha256 { get; init; }`); nothing else on the snapshot moved.

      ⚠ 2026-09-26, the guards audit's F7: only the length of the three hashes was
      checked, and the recorded script hash covered the entry script, not the modules
      it imports (the SI factors in `common.py`, the derived fields and the station
      guard in `cea_cases.py`). A fixture whose `scriptSha256` was replaced by 64 zeros
      left `Fixtures.Tests` 26/26 green.
- [x] 2026-09-30 — The outputs are bound to the generator, not only the scripts (the second
      hidden-defect audit of 2026-09-28, guards part, observation O5). The provenance
      hashes prove which scripts produced the fixtures. A hand-edited expected value
      passed every guard, and only review enforced the root's taboo on typed expected
      values.
      - The CI workflow gains a step on the Linux hosted runner (`.github/workflows/
        ci.yml`, "Fixtures are bound to the generator", after the protocol lint, before
        the build; ⚠ 2026-09-30: it moved to `windows-latest`, see the last criterion): it installs the pinned `cea` 3.3.4 and `requirements.txt` into a
        fresh virtual environment and runs `regenerate.py --check --sample`. The sample
        is chosen by the script (`regenerate.py`'s new `build_sample`) from the
        committed directory listing, at least one case per family and every case of
        the `throat` family (`SAMPLE_IN_FULL`). The check fails the job on any
        difference beyond the tolerance of the family's rule, the same comparison
        `regenerate.py --check` already made over the whole set (⚠ 2026-09-30: it was
        exact text, not "the tolerance of the family's rule", and it is now a document
        comparison with the tolerances of `tolerances.json`, see the last criterion).
      - `regenerate.py` had no case-level sampling mode; it gained one
        (`generate/writer.py`'s `Writer.only_cases`, `wants_case`, and the stale sweep
        of `finish()` turned off under it, since a sample deliberately produces only
        part of each kind and every file it does not touch is not stale for that
        reason).

        ⚠ 2026-09-28: touching `writer.py` and `regenerate.py` moved every fixture's
        `generatorSha256` (`generate/BOOT.md`'s rule hashes every `*.py` file of
        `generate/` together), so this change re-provenances the whole set. Regenerated
        with the full driver (`regenerate.py`, no filter): 328 files written, each
        differing from its previous committed content by exactly one line
        (`git diff --numstat`: `1 1` for every file), the `generatorSha256` line: no
        `case` or `outputs` field moved. `regenerate.py --check` (the full set) and
        `regenerate.py --check --sample` both exit 0 afterward.
      - Red once, run locally with the pinned interpreter (`cea` 3.3.4, `numpy`
        2.5.3 already on the machine, matching `requirements.txt`): the sampled `hp`
        case's committed `outputs.temperature` was edited by +1 K
        (`cases/hp/ap-htpb-al-fuelrich_of0.5_pc7MPa.json`, the case `build_sample` picks
        for the `hp` family), and `regenerate.py --check --sample` exited 1, naming
        exactly that file (`"changed   hp\ap-htpb-al-fuelrich_of0.5_pc7MPa.json"`);
        reverted (regenerated, since a plain `git checkout` would have restored the
        pre-re-provenance content) and confirmed clean again (`regenerate.py --check`
        and `--check --sample` both exit 0; `git diff --numstat` unchanged at `1 1` per
        file).
      - Accepted 2026-09-30 on the CI run named under the last criterion of this group: the
        step "Fixtures are bound to the generator" ran on `windows-latest` and passed.
- [x] 2026-09-30 — The sample of the binding step covers every script (the third audit pass of
      2026-09-28, part 2, finding 4c). The sample of the criterion above took one case
      per kind directory, 25 of 335 files: `rp1311.py`, `low_temperature.py`,
      `condensed_phase_limit.py`, `retention_threshold.py` and `propellants.py`'s
      rocket, hp and tp outputs were never compared, and a kind whose first file no
      script produces was skipped silently, since the stale sweep is off under a sample
      and `finish()` returned 0 with nothing compared.
      - `build_sample` (`regenerate.py`) now reads each committed file's own
        `generator.script` field, the same provenance field `Writer.case` writes, and
        picks one case per (script, kind) pair instead of one per kind directory, plus
        every case of `SAMPLE_IN_FULL` (`throat`) as before: 36 of 336 files, over 19
        (script, kind) pairs (18 with a name, `throat`'s own 18 cases counted as one).
      - `main()` verifies that coverage after generation
        (`check_sample_coverage`): `--check --sample` fails when a (script, kind) pair
        the sample intended to cover produced no comparison at all — the script no
        longer writes the exact case name the sample picked from the committed
        listing, a real gap the old, silent empty comparison passed — and when the
        whole sample is empty.
      - The comparison is exact text, as `regenerate.py --check` already makes it; the
        CI step's comment (`.github/workflows/ci.yml`, "Fixtures are bound to the
        generator") now says so instead of "beyond the family's own tolerance rule".
        ⚠ 2026-09-30: exact text no longer holds for `--check --sample`: the first CI
        run, on Linux, could not reproduce the Windows files as text, and the sample is now
        compared as documents with a tolerance (the last criterion); plain `--check` is
        still exact text.
      - Touching `regenerate.py` re-provenances every fixture (`generate/BOOT.md`'s
        rule hashes every `*.py` file of `generate/` together). Regenerated with the
        full driver (`py -3 regenerate.py`, no filter; the pinned `cea` 3.3.4 and
        `numpy` 2.5.3 already on the machine, matching `requirements.txt`): 336 files
        written, each differing from its previous committed content only in
        `generatorSha256` and `generatedOn` (`git diff` over `tests/Fixtures/cases`:
        every changed line is one of those two keys; no `case`, `outputs` or
        `scriptSha256` line added, removed or changed, confirmed by grepping every
        changed line for a different key and finding none). `regenerate.py --check`
        (336 unchanged) and `regenerate.py --check --sample` (36 unchanged) both exit 0
        afterward.
      - Red once, run locally with the same interpreter: the sampled `hp` case that
        `propellants.py` now covers on its own
        (`cases/hp/ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber.json`, a file the old
        sample never touched) had its `outputs.temperature` edited by +1 K;
        `regenerate.py --check --sample` exited 1, naming exactly that file
        (`"changed   hp\ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber.json"`, fixtures:
        changed 1, unchanged 35); restored by regenerating that kind
        (`py -3 regenerate.py hp`, since a plain `git checkout` would have restored the
        pre-re-provenance content) and confirmed clean again (`regenerate.py --check`
        and `--check --sample` both exit 0 again; the file's `git diff` matches every
        other file's pattern, `generatorSha256` and `generatedOn` only).
      - Accepted 2026-09-30 on the same CI run: the sample of 36 files over 19 (script, kind)
        pairs compared at least one case per pair, the coverage check held.
- [x] 2026-09-30 — The binding step runs on the fixtures' own platform and compares with a tolerance
      (2026-09-30, the first CI run of the binding step, on `ubuntu-latest`; the owner
      decided the same day). The two criteria above stood on an untested premise: that
      `regenerate.py` reproduces the committed files, exact text, on a hosted runner. It
      does on the reference machine (`--check`, 0 files changed) and it cannot on Linux.

      ⚠ 2026-09-30, what the Linux run measured (WSL2 Ubuntu 24.04, `cea` 3.3.4 and
      `numpy` 2.5.3 from `requirements.txt`, Python 3.12 there and 3.14 on the runner,
      the same result and the same abort on both): the reference is platform-dependent.
      - `cea` aborts (`CEA_FORTRAN_ABORT`, "Re-insertion of NaCL(cr) likely to cause
        singular matrix", or "did not converge") on `naclo4_T500`, `naclo4_T800` and
        `kclo4_T500`, the near-singular salt states, which converge on Windows. Every
        `kind` invocation of `regenerate.py` runs every script, so one abort stops the
        step.
      - Of the 333 fixture files, 253 differ in numbers from the Windows files at 1e-15
        relative or more, 142 at 1e-12, 9 at 1e-10, 8 at 1e-8, 5 at 1e-6: the `throat`
        family (up to 2.2e-5 in c* and the throat figures its search finds) and
        `rp1311-example5_T300_p70bar` (1.1e-8). One case, `beo-h2o-throat_pc15MPa_h-11.06875MJkg`,
        differs by 0.855 and in its `guardError`, because its plateau makes the search land
        elsewhere.
      - The provenance keys `thermoLibSha256` and `transLibSha256` hash a binary the
        package builds per platform: they differ on Linux by construction.
      The committed fixtures are the Windows package's, as the root's platform constraint
      says of every reference record.

      - **Where.** The step runs on `windows-latest` only (`if: matrix.os ==
        'windows-latest'`), in a fresh virtual environment with the pinned packages, after
        the protocol lint and before the build. On Linux nothing regenerates.
      - **How.** `regenerate.py --check --sample` compares a regenerated case with the
        committed file as a document, not as text: the same keys in the same order, every
        string, boolean and null equal, every number within 1e-9 relative, the throat
        family's search-derived numbers within the `throat` row of `tolerances.json` (the
        coder reads the row; the Linux figures above, at most 2.2e-5 excluding the
        `beo-h2o` case, are the evidence for any figure it must pick), and the provenance
        keys `generatedOn`, `thermoLibSha256` and `transLibSha256` left out, every other
        provenance key equal. Without `--sample` the whole set is still compared as exact
        text, on the reference machine.
      - **Kept.** Every (script, kind) pair of the sample compares at least one case; an
        empty comparison fails; the sample takes every case of the `throat` family.
      - **Evidence.** Locally: `--check` and `--check --sample` exit 0; a hand edit of one
        output number by 1e-8 relative fails the sample naming the file and the path of
        the field, by 1e-10 passes (the tolerance stated, not hidden); an edit of a
        string, of a key and of an input each fails. Then the CI run of `windows-latest`
        (below, green). If the hosted Windows CPU moves a search-derived
        number past its row, the row is recorded here with the measured figure, and never
        widened past the family's own tolerance-table value.

        Local evidence, 2026-09-30, on the reference machine (Windows, `cea` 3.3.4,
        `numpy` 2.5.3), at `f382cd1`.

        CI evidence, 2026-09-30: run 36734450932 of `71e389c`, the step on `windows-latest`
        passed against the committed Windows files with the tolerances of
        `tolerances.json` unchanged (no row widened), and was skipped on `ubuntu-latest`
        as designed; the whole job is green on both.
        - The generator's comparison is `generate/document_comparison.py`
          (`DocumentComparison`), which `writer.py`'s `Writer(tolerant=True)` uses under
          `--check --sample` only. It reads its two tolerances from `tolerances.json`
          (the `regeneration` row, 1e-9 relative, and the `throat` row, below), both added
          by this change: the `throat` row the design named did not exist in the table.
        - The `throat` row is 5e-5 relative. It applies to the numbers the search derives:
          every number of the throat station of a `throat` document, the
          `characteristicVelocity` of each of its stations (the throat's c*, which the
          chamber station repeats) and the numbers of `outputs.packageRocketThroat`; every
          other number of the family, the chamber's included, takes 1e-9. The figure is
          2.3 times the largest Linux spread (2.2e-5, above, excluding the `beo-h2o`
          case) and half of the 1e-4 the table gives the same fields against the
          reference; its derivation is in the row.
        - `regenerate.py` (the full driver, `py -3`, no filter) rewrote all 336 fixtures,
          every one changed only in `generatorSha256` and `generatedOn` (a field-by-field
          comparison of each file against its content at `e85a2de`, those two keys removed
          from both sides: 336 files, 0 differing; the line diff is 2 lines per file).
          `regenerate.py --check` then reports `unchanged 336` and `--check --sample`
          `unchanged 36`, both exit 0.
        - Each mutation applied alone to a committed file and reverted by regenerating
          (`regenerate.py --sample`, which rewrites only the sampled files; the file is
          then byte-identical to the committed one), on
          `cases/hp/ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber.json` (one of the
          sample's) and, for the family's rule, `cases/throat/lif-throat_pc7MPa.json`:
          - `outputs.temperature` by 1e-8 relative: exit 1, `changed
            hp\ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber.json` and
            `outputs.temperature: committed 3388.64832247193, regenerated
            3388.6482885854475, relative difference 1e-08 above 1e-09`;
          - the same by 1e-10 relative: `--check --sample` exit 0, `unchanged 36`, while
            plain `--check` names the file `changed` (exact text);
          - the string `generator.version` edited: exit 1 naming `generator.version`;
          - the key `outputs.entropy` renamed: exit 1, `outputs: keys differ, only
            committed ['entropyX'], only regenerated ['entropy']`;
          - the input `case.inputs.pressure` edited: exit 1 naming `case.inputs.pressure`;
          - the throat station's temperature (`outputs.stations[1].temperature`) by 1e-4:
            exit 1 naming that path against 5e-05; by 3e-5: exit 0; the chamber's
            `characteristicVelocity` and a throat mole fraction by 3e-5: exit 0; the
            chamber's `temperature` by 1e-8: exit 1 against 1e-9 (the wider rule does not
            reach the chamber's other numbers); `outputs.packageRocketThroat.cStar` by 1e-4:
            exit 1;
          - provenance: `generator.thermoLibSha256` edited: exit 0; `generator.dataThermoSha256`
            edited: exit 1; `generator.generatedOn` edited: exit 0 (the writer's own date
            rule keeps a date-only difference from being rewritten, so that one file was
            restored with the mutator, not by regenerating).
        - Strings that quote numbers (review of 2026-09-30, closing the exposure this
          bullet first recorded): `outputs.packageRocketThroat.guardError` of a case whose
          package rocket throat is not usable holds the package's full-precision numbers
          in its text (12 of the `throat` fixtures, `beo-h2o-throat_pc15MPa_h-11.06875MJkg`
          one of them), so an exact string comparison would fail all of them on a
          last-bit difference of a hosted CPU. A string with a number token is now its
          skeleton (the text with every token replaced) plus its tokens: the skeletons
          must be equal, so the words and the count of tokens are, and each pair of tokens
          is within the tolerance of the string's field class (the `throat` row under
          `outputs.packageRocketThroat` and the throat station, else `regeneration`).
          The tokens are matched by one regex, `NUMBER_TOKEN` in
          `document_comparison.py`. One narrowing of the design: a token with no decimal
          point is compared as text, since the digit runs of a hash, a version or a
          species name are not measurements, and a relative tolerance on a long digit run
          would let a changed hash through. A string with no number token is exact.
          Evidence, on the `guardError` of `beo-h2o-throat_pc15MPa_h-11.06875MJkg`, each
          applied alone and reverted by `regenerate.py --sample` (file byte-identical):
          its first decimal token by 1e-6 relative: exit 0; by 1e-3: exit 1 naming
          `outputs.packageRocketThroat.guardError` and the token; the word `away` changed
          to `far`: exit 1; a token added: exit 1; a token dropped: exit 1. The full
          regeneration was run again after this change (the generator directory's hash
          moved): 336 files, 0 differing beyond `generatorSha256` and `generatedOn`,
          `--check` `unchanged 336`, `--check --sample` `unchanged 36`. Remaining honest
          risks for the CI run: a hosted CPU that lands the mass-flux search on a different
          plateau state, as the Linux run did for `beo-h2o` (a difference of the words or of
          the numbers beyond 5e-5, which no row covers by design), and any number above the
          rows that the hosted CPU moves more than 1e-9.
      - **Records.** `tests/Fixtures/generate/API.md` names the two comparison modes;
        the comment of the CI step says what it compares and where; the ⚠ above is the
        record of the platform property, and the root's platform constraint is the
        owner's to extend if it should name the fixtures (a proposal, AGENTS.md §11).

## Taboos

- No hand-edited fixture, no fixture from an unpinned package version.
- No tolerance decided in a test node for a comparison with the reference.
- No fixture without provenance fields.
- No fixture whose inputs differ from the case matrix without the matrix being updated first.
