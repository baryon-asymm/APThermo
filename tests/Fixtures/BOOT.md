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
  version string the package reports, the script name and its SHA-256, the SHA-256 of
  the package's `thermo.lib` and `trans.lib`, the SHA-256 of the tree's `data/thermo.inp`
  and `data/trans.inp`, and the generation date.
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

Outside the tree: Python 3.11+, `cea` 3.3.4 and `numpy` (pinned in
`generate/requirements.txt`); the tree's `data/` files; xunit is not used here (the
loader is a plain library).

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Layout: `generate/` holds the Python scripts and `requirements.txt`; `cases/<kind>/`
  holds one JSON file per case (`kind` is `tp`, `hp`, `sp`, `rocket`, `transport`,
  `thermo`, `constants`); the C# loader is the node's assembly
  `AerospacePropellantThermodynamics.Fixtures`.
- Fixture document: `{ "case": { inputs by name, SI }, "generator": { provenance },
  "outputs": { by name, SI } }`; station and performance fields use the names of the
  tree's result types; compositions are mole fractions by species name, all species
  the reference reports, no threshold.
- Case matrix of version 1:
  - RP-1311 examples 1 (tp), 3 (hp, two fuels), 5 (hp, solid with a custom binder and
    condensed products), 8 (rocket LOX/LH2), 12 (rocket MMH/NTO, shifting and frozen
    with freezing at the throat), 14 (tp at low temperature with condensation), each
    from the package's own `rp1311` sample scripts, unchanged in inputs.
  - LOX/LH2 (`O2(L)` at 90.17 K, `H2(L)` at 20.27 K): O/F 4.0, 5.0, 6.0, 7.0, 8.0;
    chamber pressure 5, 7, 10 MPa; area ratios 20 and 77.5; shifting, frozen at
    chamber, frozen at throat; transport on.
  - LOX/RP-1 (`O2(L)`, `RP-1` at 298.15 K): O/F 2.0, 2.3, 2.6, 2.9, 3.2; 7 and 10 MPa;
    area ratios 16 and 40; shifting and frozen at throat; transport on.
  - NTO/UDMH (`N2O4(L)`, `C2H8N2(L),UDMH`, both at 298.15 K): O/F 1.8, 2.0, 2.2, 2.4,
    2.6; 1 and 2 MPa; area ratios 10 and 50; shifting and frozen at throat.
  - AP/HTPB/Al (`NH4CLO4(I)` 68 %, HTPB 14 %, `AL(cr)` 18 % by mass, all at
    298.15 K): 5 and 7 MPa; area ratios 8 and 12; shifting; condensed products expected.
  - Equilibrium-only cases derived from every rocket station: a tp case at the
    station's (T, p), an hp case at (h, p), an sp case at (s, p), with the station's
    composition as the reference, so that `Equilibrium` is verified without the nozzle.
  - `thermo`: `Cp°/R`, `H°/RT`, `S°/R` for a species list covering gaseous and
    condensed records, at 200, 298.15, 500, 1000, 1000.0001, 2000, 3000, 5000,
    6000 K, from an independent Python evaluation of the polynomials read from
    `data/thermo.inp` (not through the package), plus the package's `R` in `constants`.
  - `transport`: pure-species fit values from an independent Python evaluation of
    `data/trans.inp`, and mixture transport at the rocket stations from the package.
- Tolerance table, provisional until the first full comparison calibrates it
  (derived from the reference's print precision in its own sample output and its
  convergence criteria; every entry is confirmed or reworded in the acceptance criteria):

  | Field | Absolute | Relative |
  |---|---|---|
  | temperature | 0.05 K | 2e-5 |
  | pressure at derived stations | — | 1e-5 |
  | density, enthalpy, entropy, molar mass, heat capacities, `γ_s`, sound speed | — | 1e-4 |
  | mole fractions | 5e-6 | — (species below 5e-6 in the reference: only "below 1e-5" is asserted) |
  | `c*`, `Isp`, `Ivac`, `C_F`, area and pressure ratios | — | 1e-4 |
  | transport properties, Prandtl numbers | — | 5e-4 |
  | `thermo` function fixtures | — | 1e-12 |

## Acceptance criteria

- [ ] Every fixture regenerates byte-identically from the committed scripts with the
      pinned package on the reference machine (a regeneration script exits non-zero on
      any difference; date, script name).
- [ ] The loader reads every fixture and the tolerance table, and a fixture missing a
      field of its kind fails the loader with the file name (date, test name in the
      protocol tests node or here).
- [ ] The HTPB definition is decided and cited. Proposal: formula
      C 7.3165 H 10.3416 O 0.0674, enthalpy −250 cal/mol (−1046.0 J/mol) at 298.15 K,
      the definition used by common CEA front ends; to be confirmed against a cited source.
- [ ] The `data/` files are tied to the package's data: proposal, commit `data/thermo.inp`
      and `data/trans.inp` from the nasa/cea tag that produced the 3.3.4 wheel and record
      the tag in `data/NOTICE`; the generator asserts that the package's species list for
      each case equals the one `Data` reads from the tree's files.
- [ ] Tolerances calibrated after the first full comparison; every entry confirmed or
      reworded with the reason, with the date.

## Taboos

- No hand-edited fixture, no fixture from an unpinned package version.
- No tolerance decided in a test node for a comparison with the reference.
- No fixture without provenance fields.
- No fixture whose inputs differ from the case matrix without the matrix being updated first.
