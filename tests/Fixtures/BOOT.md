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
  `independent-evaluation`, or `cea-package-mass-flux-scan` for the throat family of
  the case matrix, 2026-09-27), the script name and its SHA-256, the SHA-256 of the
  package's `thermo.lib` and `trans.lib`, the SHA-256 of the tree's `data/thermo.inp`
  and `data/trans.inp`, and the generation date. The date is the day the content last
  changed: the writer leaves a fixture untouched when the regenerated document differs
  from the committed one only in the date, so regeneration is byte-identical.
  → HISTORY.md#provenance-invariant-wording
- **Fixtures are generated, never edited.** A regenerated fixture that differs from
  the committed one is a finding about the data or the package version, recorded in
  this node's acceptance criteria, not a silent update.
- **One tolerance table**, in `tolerances.json`, with a derivation per entry; no test
  node keeps a tolerance for a comparison with the reference. It also holds entries
  that are no comparison with the reference: `moleFractionFloor`, the mole fraction
  below which the GPU/CPU and union-batch comparisons assert nothing, and
  `polishThresholdRelative`, the second tier derived from the equilibrium solver's
  polish threshold (both shared by two test nodes, neither of which may read from the
  other); and, since 2026-09-30, `regeneration` (1e-9) and `throat` (5e-5), read by
  the generator's document comparison, so that no script decides a tolerance.
  `ToleranceTable.MoleFractionField` picks `moleFraction` or `moleFractionTrace` for a
  reference value, so the print threshold is written once, in the table.
  ⚠ 2026-09-30: was two such entries, now four → HISTORY.md#tolerance-table-entries
- **The case matrix is explicit** (Constraints) and file names encode the case; a test
  enumerates a directory, it never lists cases by hand.
- **Units in fixtures are SI**, converted once in the generator from the package's
  units (bar, kJ/kg, kJ/(kg·K), kg/m³, m/s), and the conversion factors are named in
  the generator's header.
- **Multi-station rocket references are guarded.** The package's rocket solver can
  err silently after a melting plateau (a liquid kept below its range at later
  stations, stations drifting off the chamber isentrope: 0.61 % of Ivac on example 13
  without its insert list; found 2026-09-13 against tp re-solves of the package).
  Every shifting station of a generated rocket reference is therefore checked before
  the fixture is written (a frozen station keeps the freezing station's composition
  by construction): no condensed species outside its joined record range unless its
  same-formula partner stands beside it (a pinned pair), and at every shifting
  station without such a pair a tp re-solve of the package at the station's (T, p)
  reproduces the station's entropy to 1e-6 relative (at a pinned station the tp state
  is degenerate and proves nothing). A station that fails is regenerated as a direct
  single-exit case from the chamber; a case that still fails is not committed. The
  guard is `guard_stations` in `generate/cea_cases.py`, run on every rocket solve,
  one log line per solution. → HISTORY.md#multi-station-guard

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
  `transport`, `thermo`, `constants`, `reactant`, `seeded`; `throat` since 2026-09-27,
  `reactant` since 2026-09-28, `seeded` since 2026-10-03); `tolerances.json` is the
  tolerance table; the C# loader is the node's assembly `APThermo.Fixtures`.

  The `seeded` kind holds hp and sp equilibrium cases that neither the reference nor the
  tree converges from a cold start, only from a converged neighbour: its inputs carry
  `"seed": {"kind": "tp", "temperature": …}` on the same reactants, and the generator
  solves the seed and then the case in one `EqSolution` of the package. Its first use is
  the Al(OH)3/Al2O3/H2O(L) reaction plateau (the StateRecord node's pinned set). There the
  reference recognises no plateau and reports frozen second-order fields: a consumer
  compares the first-order fields and the mole fractions and skips the six second-order
  fields on the reference's singular signature (`cpEquilibrium == cpFrozen` exactly, with
  a condensed species present), as the singular tp case already does.

  ⚠ 2026-09-28: the `reactant` kind was added for a reactant-level fact (`Br2(cr)`),
  not an equilibrium solve → HISTORY.md#reactant-kind

  ⚠ 2026-09-28: was "a full run would move about 318 cases by 1e-7", now provenance
  keys only, no output value → HISTORY.md#reactant-provenance-only
- Procedure: `python -m venv tests/Fixtures/generate/.venv`, install
  `requirements.txt` into it, then `python tests/Fixtures/generate/regenerate.py`
  (writes changed fixtures, removes stale ones) or `regenerate.py --check` (compares
  only, as exact text, exit code 1 on any difference). `regenerate.py --check --sample`
  compares a sample as documents, field by field, with a tolerance (the last criterion
  of ACCEPTANCE.md, `generate/API.md`). Each script also runs standalone and sweeps
  only the kinds it produces. → HISTORY.md#procedure-wording

  ⚠ 2026-09-27: was "each script sweeps only the kinds it produces", now only for a
  kind exactly one script writes; a shared kind goes through the full driver
  → [generate/BOOT.md](generate/BOOT.md)
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

  ⚠ 2026-09-12: the package's `of_ratio_to_weights` holds the oxidizer-to-fuel ratio
  in single precision before it splits the kilogram (2.6 becomes 2.5999999046), so
  the recorded mass fractions, `elementMoles` and reactant enthalpies carry up to 6e-8
  relative of that rounding against the nominal ratio (`oxidizerToFuelRatio`); a ratio
  exact in single precision (LOX/LH2 at 4, 5, 6, 7, 8) carries none. A tree that
  splits in double precision reproduces the recorded mass fractions to 1e-7 and, from
  the recorded mass fractions themselves, the element moles and enthalpies to
  rounding. → HISTORY.md#of-ratio-single-precision

  ⚠ 2026-09-12, three caveats of the reference's fields:
  - `mixtureMolarMass` (the package's `MW`) is one kilogram over the moles of all
    species, the condensed ones counted as moles; `molarMass` (its `M`) is one
    kilogram over the gaseous moles.
  - `cvFrozen` and `cvEquilibrium` at a frozen station are not computed by the
    reference: the exits carry 0 and a frozen throat carries the chamber's values.
    Test nodes do not compare them at frozen stations.
  - With transport on, `cp_fr` at every station is the frozen heat capacity of the
    package's transport set (at most 40 gaseous species, chosen as the Transport
    `BOOT.md` describes) per kilogram of that gas, and `cv_fr` is that value minus
    n R with n the gaseous moles of the whole mixture; with condensed species they
    are gas-phase values (AP/HTPB/Al chamber: 2053.8 with transport, 1914.7
    kJ/(kg·K)·10⁻³ without; ⚠ 2026-10-02: was 2038.5 and 1904.5, the figures of the
    provisional HTPB, now those of the cited one → ACCEPTANCE.md, the HTPB criterion), while cp_eq, γ_s, M and MW do not change. Hence the
    derived equilibrium cases are generated without transport, the performance tests
    compare `cpFrozen` and `cvFrozen` as gas-phase values, and the Transport tests
    compare the field with the set's heat capacity at every station with transport
    (without condensed species the set covers the gas to 1e-6).
    → HISTORY.md#reference-field-caveats

  ⚠ 2026-09-12, two more caveats found by the Transport node:
  - The package estimates the transport properties of a gaseous species without an
    entry in `trans.inp` (hard spheres for the viscosity, the modified Eucken
    relation for the conductivity, as CEA2 did); it excludes nothing.
  - The package's reacting conductivity is defective at a station where one of the
    component species that seed its transport set is a trace (x < 1e-10 of the set):
    it reports 170 to 440 times the frozen one, and `reactingPrandtl` is deflated
    (0.34–0.41 against 0.53–0.61). Nine stations of the committed fixtures carry it.
    The fixtures are not edited (first invariant); the Transport tests skip
    `reactingConductivity` and `reactingPrandtl` where the tree's solver reports a
    trace elimination, assert the defect is still visible there, and fail when it is
    gone, so that a regenerated reference removes this caveat rather than hiding it.
    → HISTORY.md#reference-transport-caveats

  ⚠ 2026-09-13: at a tp assigned exactly at a bound two records of one substance
  share, the package's derivative matrix is singular and it prints zero equilibrium
  heat capacities with `γ_s = −1/dlnVdlnP`; the composition converges and matches the
  tree's (`rp1311-example13-mixture_T2373`). The fixtures are not edited (first
  invariant); the equilibrium comparisons of the Equilibrium and Problems tests skip
  the second-order fields — `cpEquilibrium`, `cvEquilibrium`, `gammaS`, `dlnVdlnT`,
  `dlnVdlnP`, `soundSpeed` — exactly where a tp fixture prints `cpEquilibrium` 0, a
  value no real tp state has, so a regenerated reference without the defect resumes
  the full comparison by itself. → HISTORY.md#singular-tp-defect
- Case matrix: explicit, one file name per case, owned by the generator and held in
  [generate/BOOT.md](generate/BOOT.md), which only its scripts are bound by. The
  families: RP-1311 examples 1, 3, 5, 8, 12, 13 and 14; the LOX/LH2, LOX/RP-1,
  NTO/UDMH and AP/HTPB/Al rockets; the melting-plateau cases; equilibrium-only cases
  derived from stations; a sodium case; Rules A and B; the `throat` family; the
  `thermo` and `transport` function fixtures; the `reactant` case.
- Tolerance table: `tolerances.json` alone (the invariant above), derived from the
  reference's print precision in its own sample output and its convergence criteria,
  every entry confirmed or reworded by the calibration criterion of 2026-09-12
  (ACCEPTANCE.md). The typed copy of its rows stood here → HISTORY.md#tolerance-table-copy

## Shape exceptions

Added 2026-09-14 by the design session, after the protocol tests node's measurements found
this constructor over the root's six parameters. `Provenance` mirrors, field for field, the
`generator` block every fixture file carries. On the root's condition for such a type its
one creation names its arguments (the criterion of 2026-09-15 in ACCEPTANCE.md).
→ HISTORY.md#shape-intro-wording

⚠ 2026-09-15: was "passes them by position today", now each argument reads its own
named `generator` field in `CeaFixtures.ReadProvenance` → HISTORY.md#provenance-named

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Provenance.Provenance` | parameters | 12 | the `generator` block of a fixture file, field for field; its one creation names its arguments |

⚠ 2026-09-27: was 11 parameters, now 12 (`generatorSha256` added), still one site
→ HISTORY.md#provenance-twelve

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No hand-edited fixture, no fixture from an unpinned package version.
- No tolerance decided in a test node for a comparison with the reference.
- No fixture without provenance fields.
- No fixture whose inputs differ from the case matrix without the matrix being updated first.
