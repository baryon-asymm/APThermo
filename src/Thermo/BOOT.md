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

  ⚠ 2026-09-14, a declared deviation (`AGENTS.md` §12) from this invariant and from
  the root's first: the sum of `H°/RT` is written twice, once over the table view for
  the kernels and once over a `Data` record interval for the builder's join-and-cut
  test, sharing only the per-term helper. The builder needs no accelerator and ILGPU
  1.5.3 gives no view over a managed array outside a kernel (the ⚠ under Constraints),
  so the host-side sum cannot call the kernel-side one. What replaces the invariant
  there: the tests node pins the two overloads to each other bit for bit over the
  thermo fixtures' species and temperatures, so a drift cannot hide below the cut
  threshold. What would lift it: a builder that goes through the CPU accelerator,
  which this node avoids on purpose. Found by the clean-code review (F-TD-13,
  F-AR-06); until then the code's comment claimed the formula lived once.
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

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The
builder is one public entry over internal stages, one class per file in this
directory and namespace; the functions and the view keep their code, and the arrays a
table is built into are bit for bit those of `8e36a27`: the tests node's bit snapshot
(the acceptance criteria below) is the proof.

| Type | Responsibility | Visibility |
|---|---|---|
| `SpeciesTable` | the contract; `Build` reduced to the sequence: check the request, resolve each name to a gas entry or to condensed pieces, concatenate gaseous then condensed, check the limits, flatten, construct | public, contract grown by `PieceOf` |
| `TableRequest` | the request is well formed: counts, `TableLimits`, duplicate elements and species, each refused by name; the element index | internal |
| `CondensedAssembly` | the join (the records of one name are one contiguous piece, or the name is refused) and the cut (a shared bound with `|ΔH°/RT| ≥ LatentHeatThreshold` starts a new piece named `NAME[TLow-THigh]`) | internal |
| `TableLayout` | the flat layout in one place: the strides and slots (the bounds stride 2, the exponents per interval 8, the coefficient stride 9, the `b1` and `b2` slots) as constants the writer and the reader (`SpeciesFunctions`) both use, and the flattening of the pieces into `SpeciesTableArrays` | internal |
| `TablePiece` | one table species in the making: the name, the record that provided its first interval, its intervals (today's private entry record, promoted so that the stages can pass it) | internal |
| `SpeciesFunctions` | code unchanged, reading the layout through `TableLayout`; gains `RecordLow` and `RecordHigh` (below) | public |

Decisions taken with the reviews of 2026-09-14:

- **The table answers the range questions.** Two neighbours re-derived this node's
  interval layout: the front door found the piece of a cut record covering a
  temperature, and the equilibrium solver read a record's first lower and last upper
  bound from the arrays (the architecture review's F-AR-01). The contract gains
  `SpeciesTable.PieceOf(string species, double temperature)` (host side, the piece by
  the same rule as `IntervalOf`) and `SpeciesFunctions.RecordLow(in view, int)` and
  `RecordHigh(in view, int)` (kernel-compatible, the bounds `IsInRange` compares);
  they evaluate the identical expressions, so nothing moves. The neighbours switch to
  them in their own tasks. Recorded in `API.md` with its ⚠; the snapshot moves in the
  same commit.
- **The duplicate-name records come from `Data`.** The builder no longer rebuilds a
  name → records index over the whole product list on every call: `Data` publishes
  the records of a name in file order (`SpeciesDatabase.Records`, its own decision of
  the same day), and the sentence under Constraints about the database index
  returning the first record per name now points at the neighbour's contract
  instead of restating it.
- **The constructors of the view and the arrays are the declared exception** to the
  parameter rule: `SpeciesTableView` (11 parameters) and `SpeciesTableArrays` (8) are
  the layout itself, the aggregation mechanism the root names for kernels; eight of
  the eleven are caught by the type system on a swap, and splitting them into column
  structs would change the contract of four kernel nodes for no numerical gain.
- **The join compares the formation enthalpy too**, if the committed file lets it:
  the same-name product groups are scanned first; where none disagrees, a disagreeing
  pair is refused like a differing formula or molar mass; where one does, the rule is
  recorded here instead and the first record's value stands (the review's F-TD-09).

  Confirmed 2026-09-14 by a scan of `data/thermo.inp` (2 030 product records; the
  scan's own count matches `ThermoLoadTests.Every_record_of_the_file_is_parsed`):
  ten names repeat in the PRODUCTS section — `Co(b)`, `Cr(cr)`, `Cr2O3(I)`, `Fe(a)`,
  `Fe2O3(cr)`, `Fe3O4(cr)`, `K2S(cr)`, `Na2S(cr)`, `Ni(cr)`, `SnS(cr)`, the same ten
  the concatenation list above already named — and none disagrees in
  `FormationEnthalpy`. The join therefore refuses a disagreeing pair exactly as it
  refuses a differing formula or molar mass (`CondensedAssembly.Touches`); the tests
  node exercises the refusal on a synthetic pair, since no real one disagrees
  (`JoinAndCutTests.Records_disagreeing_in_formation_enthalpy_are_refused_by_name`).
- **`MixtureMolarMass`'s summary in the code** says what `API.md` has said since
  2026-09-12: one kilogram over the moles of all species, condensed included (the
  review's F-TD-04: the rename of that day changed the field and the document and
  left the comment).
- **`SpeciesTableBuffers` stays here**; the "out of scope" line of `API.md` that
  contradicted it goes (the review's F-TD-11).
- **Size.** No method over 60 lines, no control flow nested deeper than 3, no more
  than 6 parameters (the two constructors aside).

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
- [x] 2026-09-14 — The decomposition of `## Structure`: every type of the node within
      the root's code-shape constraint (measured by hand pending the protocol tests
      node's `ShapeTests`, root `BOOT.md`; the largest new file, `SpeciesTable.cs`,
      179 lines; the two constructors declared above the only exceptions), the public
      surface grown only by `PieceOf`, `RecordLow` and `RecordHigh` (with `Records` of
      the `Data` node, the snapshot moves by exactly four lines over the whole task),
      `PublicSurface.approved.txt` moved in the same commit, and every table bit for
      bit as at `8e36a27`: the tests node's bit snapshot over every fixture case's
      table unchanged (`dotnet test tests/Thermo.Tests`, 378 tests), `KernelEqualityTests`
      and the fixture tests green, the fast suite green.
- [x] 2026-09-14 — `PieceOf`, `RecordLow` and `RecordHigh` agree with `IntervalOf` and
      `IsInRange` over every fixture species: `RangeQuestionTests`
      (`PieceOf_names_the_piece_the_interval_rule_chooses` over `ALN(L)`, the one cut
      name of the thermo fixtures, `RecordLow_and_RecordHigh_are_the_bounds_IsInRange_uses`
      over all 40). The two `H°/RT` overloads agree bit for bit over the thermo
      fixtures' species and temperatures (the declared deviation under Invariants):
      `OverloadPinningTests.Host_and_kernel_enthalpy_sums_give_the_same_bits`, 40
      species. The join refuses a same-name pair that disagrees in formation enthalpy,
      the rule the scan above decided: `JoinAndCutTests.Records_disagreeing_in_formation_enthalpy_are_refused_by_name`.

## Taboos

- No parsing of any file format: that is `Data`.
- No species selection, no propellant knowledge: indices in, values out.
- No `float`, no `LibDevice`, no ILGPU.Algorithms: the root forbids them in numerical nodes.
- No second physical constant, no atomic weight, no coefficient typed into code.
- No hidden ordering: the builder never sorts species or elements on its own.
