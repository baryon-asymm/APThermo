# BOOT.md — Thermo

## Purpose

The first kernel-capable level: the compact species tables the numerical program
consumes, the species functions `Cp°/R`, `H°/RT`, `S°/R`, `G°/RT` evaluated at a
temperature, and the vocabulary shared by every numerical node (the mixture state
record, the per-case status codes, the gas constant). `Data` gives an object model
with strings and lists; this node turns a chosen subset of it into flat arrays that
can be uploaded to an accelerator and evaluated without allocation.

## Invariants

- **Pure functions.** Every species function takes the table view, a species index
  and a temperature and returns a value; it allocates nothing, throws nothing, keeps
  no state. Results are bit-identical between two calls with the same arguments on
  the same accelerator, and agree between the CPU accelerator and CUDA within the
  math-function tolerance of the execution tests node.
- **The table is immutable and ordered.** Species indices are `0 … SpeciesCount−1` in
  the order given to the builder; gaseous species come first (`0 … GasCount−1`), then
  condensed ones; element indices are the order given to the builder. Nothing
  reorders them afterwards, because every other node addresses species by index.
- **Formulas are the NASA ones**, exactly as recorded in the `Data` node's format
  facts, with the exponents taken from the record, not assumed. One implementation
  of each formula in the tree.
- **Interval selection is defined.** For a temperature `T`, the interval used is the
  first one with `T ≤ THigh`; below the first interval or above the last, the nearest
  interval's polynomial is used and `IsInRange` reports `false`. `IsInRange` is exact
  against the record's own bounds; for gaseous species it is advisory.

  ⚠ 2026-09-13: stood "for condensed species `IsInRange` is the candidacy test other
  nodes rely on". The melting-plateau analysis of this date moved the equilibrium
  node's condensed candidacy to effective bounds — the crossing of adjacent records'
  Gibbs curves, which that node derives from this table's bounds and fits, because
  the committed fits cross up to 2.7e-3 K away from the printed bound and a pinned
  two-phase pair is exempt from any range test. `IsInRange` itself is unchanged.
- **One condensed species per contiguous fit.** The builder joins and cuts condensed
  product records so that every condensed table species is one contiguous,
  thermodynamically continuous piece: records sharing one name (the file splits some
  condensed species into one record per range) are concatenated into one species when
  their formulas and molar masses agree and their ranges touch, and a condensed
  species whose adjacent intervals disagree at a shared internal bound by
  `|ΔH°/RT| ≥ LatentHeatThreshold` is split there into separate table species — the
  equilibrium node then sees every real transition as a boundary between two records
  and never as a jump inside one. Gaseous species are never joined or split.
  (The join-and-cut section of `API.md`.)
- **One physical constant.** `R = 8314.51 J/(kmol·K)` is the value NASA CEA uses and
  the only physical constant typed into the tree; it lives here and nowhere else, and
  a test compares it with the value the reference implementation exposes.
- **The stoichiometry matrix is complete.** Every element of every species in the
  table is one of the table's elements; the builder refuses a species that brings an
  element outside the given list.

## Dependencies

- [Data](../Data/API.md) — the object model the builder flattens: species records,
  intervals, formulas, molar masses.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`, `MemoryBuffer1D<T, Stride1D.Dense>`,
and `Accelerator` as the parameter of the upload; no accelerator is created here).

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- The functions and the view are kernel-compatible C# (static methods, blittable
  structs, no allocation, no exceptions). The builder is ordinary .NET.
- Size limits are fixed here because they size the scratch of every consumer: at most
  20 elements, at most 2 048 species per table, at most 5 intervals per species
  (after concatenation; the builder refuses an overflow by name).
- The join-and-cut threshold is `SpeciesFunctions.LatentHeatThreshold` = 1e-3 on
  `|ΔH°/RT|` at a shared bound, the one constant separating a real latent heat from
  fit noise: the smallest real transition of the committed file is BeO a/b at
  1.34e-2, the largest interval-split artifact 3.9e-4 (`Cr(cr)`). It lives here
  because the equilibrium node's pair rule tests the same quantity against the same
  constant. On the committed file the cut fires exactly once — `ALN(L)`, whose two
  intervals differ by 68 kJ/mol at 2700 K, the only such jump among the 203
  multi-interval condensed product records — and the concatenation covers every
  same-name record split of the file (`Co(b)`, `Cr(cr)`, `Cr2O3(I)` — three
  records — `Fe(a)`, `Fe2O3(cr)`, `Fe3O4(cr)`, `K2S(cr)`, `Na2S(cr)`, `Ni(cr)`,
  `SnS(cr)`), whose upper records were unreachable before (the database index
  returns the first record per name).
- A cut piece is named `NAME[TLow-THigh]` over the piece's range in kelvin
  (`ALN(L)[1800-2700]`, `ALN(L)[2700-6000]`; square brackets occur in no database
  name); the pieces stand adjacent, ascending, in the place of their record in the
  given order, and `Records` maps each piece, and each concatenated species, to the
  record that provided its first interval.
- Math: only `Math.Log` and `Math.Pow` from the root's list are needed; `Math.Pow`
  is used for the general exponents, the usual exponents −2 … 4 are evaluated by
  multiplication.
- The table view is a struct of `ArrayView<double>` and `ArrayView<int>` over buffers of
  an accelerator (`SpeciesTableBuffers.Upload`). The builder produces host arrays
  (`SpeciesTableArrays`) in the same layout and needs no accelerator; evaluating the
  species functions on the host goes through the CPU accelerator's buffers, whose views
  host code may index.

  ⚠ 2026-09-12: stood "on the host, the same layout is exposed as arrays so that tests
  and the builder need no accelerator", with a `HostView` over the arrays in `API.md`.
  ILGPU 1.5.3 converts a managed array into a view only inside kernels
  (`ArrayViewExtensions.AsArrayView`: "supported in kernels only"); outside a kernel a
  view needs a memory buffer of an accelerator. The builder still needs none; the
  tests create the CPU accelerator to evaluate. Found when the view was implemented.

## Acceptance criteria

- [x] 2026-09-12 — For the species and temperatures of the `thermo` fixtures (one file
      per species under `tests/Fixtures/cases/thermo/`, generated by the fixtures node's
      independent Python evaluation of the same records), `Cp°/R`, `H°/RT`, `S°/R`,
      `G°/RT`, the interval used and the range flag equal the fixture within the
      `thermoFunction` entry of the tolerance table (1e-12 relative and absolute):
      `Thermo.Tests`, `FunctionFixtureTests.Functions_equal_the_independent_evaluation`.
- [x] 2026-09-12 — For `H2O`, `CO2`, `H2`, `N2` the values at 298.15, 1000, 2000 and
      3000 K agree with the NIST-JANAF tables (typed into `tests/Thermo.Tests/janaf.json`
      with the tables' precision and citation) within a tolerance recorded per species
      in that file with the reason: 1e-3 for `H2` and `N2` (worst deviation 2.4e-4),
      2e-3 for `CO2` (worst 1.2e-3, Cp° at 3000 K), 2.5e-2 for `H2O` (worst 1.9e-2, Cp°
      at 3000 K): `JanafTests.Fits_reproduce_the_JANAF_rows_within_the_recorded_tolerance`.

      ⚠ 2026-09-12: stood "within the fit accuracy stated by the NASA report: 0.1 %".
      That figure is the fit's accuracy against its own source data, and the sources of
      these four records are Gurvich et al. (`H2`, `N2`, `CO2`) and Woolley 1987 (`H2O`),
      not JANAF; the compilations differ from JANAF by up to 0.12 % (`CO2` at 3000 K)
      and 1.9 % (`H2O` at 3000 K, where Woolley's partition function supersedes the 1985
      table). The JANAF comparison is a plausibility check of formulas and units; the
      correctness check is the 1e-12 comparison above. Found when the test first ran.
- [x] 2026-09-12 — A bound shared by two intervals belongs to the lower one (1000 K,
      6000 K and every joint of `H2O`, `CO2`, `AL2O3(a)`, `W(cr)`), and below the first
      bound or above the last `IsInRange` is `false` while the value is the nearest
      polynomial's: `IntervalRuleTests` (`A_shared_bound_belongs_to_the_lower_interval`,
      `Outside_the_range_the_nearest_interval_is_used_and_flagged`) and the out-of-range
      points of every `thermo` fixture.
- [x] 2026-09-12 — The builder's stoichiometry matrix equals the `Data` formulas for
      every entry of the table (the list is generated from the table, not typed), and a
      species with a foreign element is refused with its name and the element's in the
      message: `TableBuilderTests` (`The_stoichiometry_matrix_equals_the_data_formulas`,
      `A_species_with_a_foreign_element_is_refused_by_name`, and the order, interval,
      limit and lookup tests of the class).
- [x] 2026-09-12 — `PhysicalConstants.R` equals the value in the fixture written by
      the reference package (`cases/constants/R.json`, `cea.R`):
      `FunctionFixtureTests.R_equals_the_reference_package_constant`.
- [x] 2026-09-12 — The functions run unchanged inside an ILGPU kernel on the CPU
      accelerator and give the same bits as the host call, for nine species at twelve
      temperatures: `KernelEqualityTests.Kernel_and_host_give_the_same_bits` (the
      execution tests node covers CUDA).
- [x] 2026-09-13 — The join-and-cut rule holds on the committed file: `Cr(cr)`
      builds as one species whose range reaches the second record's upper bound,
      `ALN(L)` builds as two species named by their ranges with the real jump
      visible across the pieces, and a species list naming records that cannot
      concatenate is refused by name (`JoinAndCutTests`, all three facts); the
      functions of the joined `Cr(cr)` and `Fe(a)` and of both `ALN(L)` pieces equal
      the independent evaluation piecewise
      (`FunctionFixtureTests.Functions_equal_the_independent_evaluation` over their
      fixtures, the generator joining records the same way); and every species of
      the four reference propellants' tables builds and compares as before (the
      Equilibrium, Performance and Problems fixture suites of the same day).

## Taboos

- No parsing of any file format: that is `Data`.
- No species selection, no propellant knowledge: indices in, values out.
- No `float`, no `LibDevice`, no ILGPU.Algorithms: the root forbids them in numerical nodes.
- No second physical constant, no atomic weight, no coefficient typed into code.
- No hidden ordering: the builder never sorts species or elements on its own.
