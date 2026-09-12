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
  node keeps a tolerance for a comparison with the reference.
- **The case matrix is explicit** (Constraints) and file names encode the case; a test
  enumerates a directory, it never lists cases by hand.
- **Units in fixtures are SI**, converted once in the generator from the package's
  units (bar, kJ/kg, kJ/(kg·K), kg/m³, m/s), and the conversion factors are named in
  the generator's header.

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
  from the file's formulas, `products` (the species list the package used), the
  problem values in SI, and for derived cases `derivedFrom`. Rocket outputs are
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
- [ ] The tree's species selection for each case equals the package's product list
      recorded in the fixture inputs (`products`); checked where the tree selects
      species (the Problems tests node), not in the generator, which cannot run the tree.

      ⚠ 2026-09-12: stood "the generator asserts that the package's species list
      equals the one `Data` reads": a Python script cannot call the tree; the fixture
      records the list and the comparison moves to the node that selects species.
- [ ] Tolerances calibrated after the first full comparison; every entry confirmed or
      reworded with the reason, with the date. 2026-09-12: the tp, hp and sp kinds (106
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
      own composition, which the Problems tests node makes. The end-to-end comparison
      remains.

## Taboos

- No hand-edited fixture, no fixture from an unpinned package version.
- No tolerance decided in a test node for a comparison with the reference.
- No fixture without provenance fields.
- No fixture whose inputs differ from the case matrix without the matrix being updated first.
