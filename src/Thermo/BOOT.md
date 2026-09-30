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
  against the record's own bounds; for gaseous species it is advisory. The record's
  bounds are the lowest lower bound and the highest upper bound over its intervals,
  taken bound by bound (2026-09-26). This is the reference's rule
  (`minval(T_fit(:, 1))` and `maxval(T_fit(:, 2))`, cea 3.3.4 `equilibrium.f90`
  1692–1693 and 1913–1915). Interval selection itself is unchanged. It is the
  reference's selection too: for `Si(cr)` cea 3.3.4 evaluates the inverted first piece
  at 298.15 K and the second interval at 299 K and 300 K, as `IntervalOf` does.
  Measured 2026-09-26 through the package's `calc_property`. It agrees to 5.7e-6
  relative in H°/RT, which is the package's older gas constant 8.31451.

  ⚠ 2026-09-26: `RecordLow` was "the first interval's lower bound" and `RecordHigh` "the
  last interval's upper bound", which assumes a record's intervals ascend. Eleven
  condensed records of the committed file begin with an inverted interval (the Data
  node's anomaly list).
  - Nine run 300 → 298.15 before a regular interval from 298.15 K (`Ca(a)`, `CrN(cr)`,
    `FeCL3(cr)`, `FeOCL(cr)`, `Fe3O4(cr)`, `Li(cr)`, `NH4F(cr)`, `Si(cr)`,
    `Ti3O5(a)`). The old rule refused them between 298.15 and 300 K, where their data
    hold and the reference admits them.
  - A tp of Si in argon at 299 K returned `Ok` with Si3 vapour (253 kJ/kg) where cea
    3.3.4 has `Si(cr)` (0 kJ/kg). The two agree to the last printed digit from 300 K on.
  - For `Br2(cr)` (one interval, 300 → 265.9) and `U3O8(II)` (300 → 300, then
    300 → 483) the two rules agree: `Br2(cr)` is in range nowhere, in the reference
    too.

  Found by the hidden-defect audit of 2026-09-26 (Thermo and Equilibrium, finding 1),
  confirmed against cea 3.3.4 the same day. The tests missed it for two reasons: the
  fixture generator used the same first-bound rule, and no fixture species had an
  inverted interval.

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
  20 elements, at most 2 048 species per table, at most 6 intervals per species
  (after concatenation; the builder refuses an overflow by name). The interval limit is
  at least the largest interval count of a product record of the committed file after
  the join, which a test computes from the file (2026-09-27).

  ⚠ 2026-09-27: stood "at most 5 intervals per species". `NaCN(II)` has six intervals
  in one record, the only such product record of the committed file, so every table
  whose elements include Na, C and N was refused. `apthermo equilibrium` on NaNO3(a) and
  RP-1 at O/F 4, 7 MPa, hp, printed "species 'NaCN(II)' has 6 intervals, more than the
  limit of 5" at `1dfc44e`, and the front door let the builder's `ArgumentException`
  through. Found by the Thermo coder of 2026-09-27 while testing the latent-heat
  threshold; the orchestrator's scan with the generator's reader confirmed that no
  other record exceeds 5.
- **`KernelMath`** (2026-09-27, the root's math constraint): `Min(double, double)` and
  `Max(double, double)` for every numerical node. They return what `System.Math.Min`
  and `System.Math.Max` return for every pair of doubles, NaN and signed zeros included,
  since that is the CPU accelerator's result. They are written with comparisons and
  selections only: NaN if either operand is NaN, −0 below +0. Both operands are
  tested for NaN before any ordered comparison (2026-09-28):

  ```text
  Min(a, b) = IsNaN(a) ? a : IsNaN(b) ? b : a != b ? (a < b ? a : b) : (IsNegative(a) ? a : b)
  Max(a, b) = IsNaN(a) ? a : IsNaN(b) ? b : a != b ? (b < a ? a : b) : (IsNegative(b) ? a : b)
  ```

  This returns what .NET 10's managed `Math.Min` and `Math.Max` return for every pair,
  and for two NaNs the first operand exactly (2026-09-30: `System.Math` itself gives no
  payload guarantee in optimized code, criterion below), and no ordered comparison inside
  them ever sees a NaN, whichever
  side ILGPU moves a constant to (the root's third ILGPU defect). The names
  `double.IsNaN` and `double.IsNegative` are allowed inside `KernelMath`, and nowhere
  else in the numerical nodes, if ILGPU compiles them without libdevice.

  ⚠ 2026-09-28: stood "following the logic of .NET's own implementation … So both
  accelerators run the same instructions". .NET's form tests only the first operand
  for NaN and lets the ordered comparison `val1 < val2` decide a NaN second operand.
  ILGPU 1.5.3 moves a constant left operand of a float comparison to the right and
  inverts its NaN ordering while doing so (`IR/Construction/Compare.cs:67-85` and
  `UpdateFlags` in `IR/Values/Compare.cs:128-140`: the toggle that is right for an
  inversion is applied to a swap), so once inlining makes `val1` a constant,
  `1.0 < v` compiles to `setp.gtu.f64` and `Min(1.0, NaN)` is 1.0 on CUDA and NaN on
  the CPU. Run on the reference device: 18 mismatches over 12 outputs and 10 inputs,
  all at the three NaN inputs. `Max` was safe in either order. The one production call
  of that shape, `DampedStep`'s `Min(lambda, …)` with `lambda` = 1.0, cannot receive a
  NaN (`largest > 0.0` guards it), so no result moved. The probe called only
  `Min(v, 1.0)` and `Max(v, 1.0)`, variable first, and the host facts run where nothing
  is swapped. Found by the second hidden-defect audit of 2026-09-28, independently by
  its Execution part (finding F1, on the device and in ILGPU's source) and its guards
  part (finding F2, by an offline PTX compile).
- The join-and-cut threshold is `SpeciesFunctions.LatentHeatThreshold` = 5e-3 on
  `|ΔH°/RT|` at a shared bound (2026-09-27). It is the one constant separating a real
  latent heat from fit noise, and it lives here because the equilibrium node's pair
  rule tests the same quantity against the same constant. The committed file, scanned
  2026-09-27 over every shared bound of the condensed product records, inside a record
  and between two records of one formula:
  - the largest fit noise is 2.2e-3, `NaCN(II)` → `NaCN(III)` at 288.5 K, a lambda
    transition, which has no latent heat; inside a record the largest is 1.34e-3,
    `NaCN(III)` at 293.15 K;
  - the smallest real transition is 1.34e-2, `BeO(a)` → `BeO(b)` at 2373 K;
  - nothing lies between the two, and 5e-3 sits at their geometric middle.

  On the committed file the cut fires twice:
  - `ALN(L)`, whose two intervals differ by 68 kJ/mol at 2700 K;
  - the joined `SnS(cr)` at 875 K (|ΔH°/RT| 9.2e-2), where the file's two records of
    that name are the rhombic and the cubic phase.

  The concatenation covers every same-name record split of the file (`Co(b)`,
  `Cr(cr)`, `Cr2O3(I)` — three records — `Fe(a)`, `Fe2O3(cr)`, `Fe3O4(cr)`, `K2S(cr)`,
  `Na2S(cr)`, `Ni(cr)`, `SnS(cr)`), whose upper records were unreachable before (the
  database index returns the first record per name).

  ⚠ 2026-09-27: stood "= 1e-3 … the smallest real transition of the committed file is
  BeO a/b at 1.34e-2, the largest interval-split artifact 3.9e-4 (`Cr(cr)`) … On the
  committed file the cut fires exactly once — `ALN(L)` … the only such jump among the
  203 multi-interval condensed product records". The scan of 2026-09-13 missed `NaCN`
  and did not count the bound a join creates. With 1e-3 the cut also fired at
  `NaCN(II)` 287.7 K and `NaCN(III)` 293.15 K, splitting each into pieces with a
  phantom latent heat of about 3 J/mol, and the pair rule pinned `NaCN(II)`/`NaCN(III)`
  at 288.5 K as a melting plateau. The reference treats each record as one species.
  The `SnS(cr)` cut is real and stays. Found by the Equilibrium coder of 2026-09-26,
  whose many-phase fixture split `SnS(cr)` and `NaCN(III)`; the orchestrator's scan
  confirmed it with the generator's own reader.
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
| `SpeciesTable` | the contract; `Build` reduced to the sequence: check the request, resolve each name to a gas entry or to condensed pieces, concatenate gaseous then condensed, check the limits, flatten, construct | internal (2026-09-15, distribution phase), contract grown by `PieceOf` |
| `TableRequest` | the request is well formed: counts, `TableLimits`, duplicate elements and species, each refused by name; the element index | internal |
| `SpeciesResolution` | one requested name resolved into its table pieces: a gas entry (its first product record, or the database's own record for a name no product carries) or, for a condensed name, the join (the records of one name are one contiguous piece, or the name is refused) and the cut (a shared bound with `|ΔH°/RT| ≥ LatentHeatThreshold` starts a new piece named `NAME[TLow-THigh]`); every name's formula checked against the table's elements first | internal |
| `TableLayout` | the flat layout in one place: the strides and slots (the bounds stride 2, the exponents per interval 8, the coefficient stride 9, the `b1` and `b2` slots) as constants the writer and the reader (`SpeciesFunctions`) both use, and the flattening of the pieces into `SpeciesTableArrays` | internal |
| `TablePiece` | one table species in the making: the name, the record that provided its first interval, its intervals (today's private entry record, promoted so that the stages can pass it) | internal |
| `SpeciesFunctions` | code unchanged, reading the layout through `TableLayout`; gains `RecordLow` and `RecordHigh` (below) | internal (2026-09-15, distribution phase) |

⚠ 2026-09-15 (distribution phase): the Visibility column read "public" for `SpeciesTable`
and `SpeciesFunctions`. The API review of that day (fixed in `e284939`) found
no consumer scenario for either: every use is a neighbour numerical node composing the
kernel layer, or this node's own tests. Both, with `PhysicalConstants`,
`SpeciesTableArrays`, `SpeciesTableBuffers`, `SpeciesTableView` and `TableLimits`, became
`internal`, with `InternalsVisibleTo` grants to the nodes that use them
(`APThermo.Thermo.csproj`; `API.md`'s tree-contract sections list them). `MixtureState`
and `CaseStatus` stay public: a consumer reads them from the result records of `Problems`.

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
  structs would change the contract of four kernel nodes for no numerical gain. Every
  creation of the two names its arguments, as the root requires of a mirrored shape
  (added 2026-09-14: a scan of the construction sites found them positional).
- **The join compares the formation enthalpy too**, if the committed file lets it:
  the same-name product groups are scanned first; where none disagrees, a disagreeing
  pair is refused like a differing formula or molar mass; where one does, the rule is
  recorded here instead and the first record's value stands (the review's F-TD-09).

  Confirmed 2026-09-14 by a scan of `data/thermo.inp` (2 030 product records; the
  scan's own count matches `ThermoLoadTests.EveryRecordOfTheFileIsParsed`):
  ten names repeat in the PRODUCTS section — `Co(b)`, `Cr(cr)`, `Cr2O3(I)`, `Fe(a)`,
  `Fe2O3(cr)`, `Fe3O4(cr)`, `K2S(cr)`, `Na2S(cr)`, `Ni(cr)`, `SnS(cr)`, the same ten
  the concatenation list above already named — and none disagrees in
  `FormationEnthalpy`. The join therefore refuses a disagreeing pair exactly as it
  refuses a differing formula or molar mass (`SpeciesResolution.Joins`); the tests
  node exercises the refusal on a synthetic pair, since no real one disagrees
  (`JoinAndCutTests.RecordsDisagreeingInFormationEnthalpyAreRefusedByName`).
- **`MixtureMolarMass`'s summary in the code** says what `API.md` has said since
  2026-09-12: one kilogram over the moles of all species, condensed included (the
  review's F-TD-04: the rename of that day changed the field and the document and
  left the comment).
- **`SpeciesTableBuffers` stays here**; the "out of scope" line of `API.md` that
  contradicted it goes (the review's F-TD-11).
- **`CondensedAssembly` is renamed `SpeciesResolution`, `Touches` renamed `Joins`**
  (2026-09-15, the clean-code repair's R-Thermo-1). The type's own summary and its
  Structure row above described it as only "the join … and the cut", but the code has
  always resolved every requested name through it, gaseous or condensed: the gas
  branch, the reactant-record fallback for a name no product carries, and the
  stoichiometry check that refuses a foreign element sit beside the join-and-cut,
  named by neither. The name now matches the scope instead of the scope being cut
  back to the name: Ce and every bit are unchanged (`SpeciesResolution` measures Ce 8,
  as `CondensedAssembly` did), only the identifiers and the two descriptions move.
  `SpeciesTable.cs`'s two references and `Thermo.Tests`' one doc-comment mention
  renamed with it.
- **Size.** No method over 60 lines, no control flow nested deeper than 3, no more
  than 6 parameters (the two constructors aside).

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `SpeciesTableView.SpeciesTableView` | parameters | 11 | the layout itself, the aggregation mechanism the root names for kernels (the decision "The constructors of the view and the arrays are the declared exception to the parameter rule"); every creation names its arguments |
| `SpeciesTableArrays.SpeciesTableArrays` | parameters | 8 | the layout itself, as `SpeciesTableView` above; every creation names its arguments |

No type of this node names more than 8 distinct types of the tree by the dependency
check's walk (`SpeciesResolution` and `SpeciesTable` tie at 8), below the root's limit
of 14: no efferent coupling row is needed.

## Acceptance criteria

- [x] 2026-09-12 — For the species and temperatures of the `thermo` fixtures (one file
      per species under `tests/Fixtures/cases/thermo/`, generated by the fixtures node's
      independent Python evaluation of the same records), `Cp°/R`, `H°/RT`, `S°/R`,
      `G°/RT`, the interval used and the range flag equal the fixture within the
      `thermoFunction` entry of the tolerance table (1e-12 relative and absolute):
      `Thermo.Tests`, `FunctionFixtureTests.FunctionsEqualTheIndependentEvaluation`.
- [x] 2026-09-12 — For `H2O`, `CO2`, `H2`, `N2` the values at 298.15, 1000, 2000 and
      3000 K agree with the NIST-JANAF tables (typed into `tests/Thermo.Tests/janaf.json`
      with the tables' precision and citation) within a tolerance recorded per species
      in that file with the reason: 1e-3 for `H2` and `N2` (worst deviation 2.4e-4),
      2e-3 for `CO2` (worst 1.2e-3, Cp° at 3000 K), 2.5e-2 for `H2O` (worst 1.9e-2, Cp°
      at 3000 K): `JanafTests.FitsReproduceTheJANAFRowsWithinTheRecordedTolerance`.

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
      polynomial's: `IntervalRuleTests` (`ASharedBoundBelongsToTheLowerInterval`,
      `OutsideTheRangeTheNearestIntervalIsUsedAndFlagged`) and the out-of-range
      points of every `thermo` fixture.
- [x] 2026-09-12 — The builder's stoichiometry matrix equals the `Data` formulas for
      every entry of the table (the list is generated from the table, not typed), and a
      species with a foreign element is refused with its name and the element's in the
      message: `TableBuilderTests` (`TheStoichiometryMatrixEqualsTheDataFormulas`,
      `ASpeciesWithAForeignElementIsRefusedByName`, and the order, interval,
      limit and lookup tests of the class).
- [x] 2026-09-12 — `PhysicalConstants.R` equals the value in the fixture written by
      the reference package (`cases/constants/R.json`, `cea.R`):
      `FunctionFixtureTests.REqualsTheReferencePackageConstant`.
- [x] 2026-09-12 — The functions run unchanged inside an ILGPU kernel on the CPU
      accelerator and give the same bits as the host call, for nine species at twelve
      temperatures: `KernelEqualityTests.KernelAndHostGiveTheSameBits` (the
      execution tests node covers CUDA).
- [x] 2026-09-13 — The join-and-cut rule holds on the committed file: `Cr(cr)`
      builds as one species whose range reaches the second record's upper bound,
      `ALN(L)` builds as two species named by their ranges with the real jump
      visible across the pieces, and a species list naming records that cannot
      concatenate is refused by name (`JoinAndCutTests`, all three facts); the
      functions of the joined `Cr(cr)` and `Fe(a)` and of both `ALN(L)` pieces equal
      the independent evaluation piecewise
      (`FunctionFixtureTests.FunctionsEqualTheIndependentEvaluation` over their
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
      (`PieceOfNamesThePieceTheIntervalRuleChooses` over `ALN(L)`, the one cut
      name of the thermo fixtures, `RecordLowAndRecordHighAreTheBoundsIsInRangeUses`
      over all 40). The two `H°/RT` overloads agree bit for bit over the thermo
      fixtures' species and temperatures (the declared deviation under Invariants):
      `OverloadPinningTests.HostAndKernelEnthalpySumsGiveTheSameBits`, 40
      species. The join refuses a same-name pair that disagrees in formation enthalpy,
      the rule the scan above decided: `JoinAndCutTests.RecordsDisagreeingInFormationEnthalpyAreRefusedByName`.
- [x] 2026-09-14 — Every creation of `SpeciesTableView` and `SpeciesTableArrays` in the
      tree names its arguments (the decision on the constructors of the view and the
      arrays), the protocol tests node's named-construction fact green once it exists;
      the tests node's bit snapshot unchanged. A scan of every `new T(…)` and
      `T x = new(…)` of the two names in `src/` and `tests/` (a script outside the tree)
      finds four sites, in `SpeciesTableView`, `TableLayout`, `Equilibrium.Tests`'
      `InvalidInputTests` and `Transport.Tests`' `StatusTests`, every argument named;
      the builds of `Thermo`, `Equilibrium.Tests` and `Transport.Tests` after the change
      carry the IL of the builds before it, method by method, so no argument binds to
      another parameter; `Thermo.Tests` (378), `Equilibrium.Tests` (463) and
      `Transport.Tests` (157) green; `tests/Thermo.Tests/Bits.approved.txt` unchanged
      (blob `8bd5068e` before and after). The fact,
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
      and not yet written; it takes over as the evidence when it is.
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-28 — The record bounds are the reference's (the invariant "Interval
      selection is defined" and its ⚠ of this date). Three of the four planned proofs
      landed on 2026-09-26 already: the equilibrium fixtures below 300 K (Si and Li in
      argon at 298.15, 299, 299.99, 300 and 301 K, covered by
      `EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`'s directory
      listing), `RangeQuestionTests.RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds`
      (over every condensed record built alone, from a database listing, red against
      the old first-interval/last-interval rule on the nine anomaly records), and no
      bit snapshot moved.

      ⚠ 2026-09-28: the remaining bullet asked for a generator change — a 299 K sample
      for the eleven anomaly records — so that the `thermo` fixture comparison itself
      exercised a point strictly between 298.15 and 300 K. The second hidden-defect
      audit's O5 found this bullet still open with the code, the fixtures and the
      generated-list fact it names already in place (the guards report of the same
      date). Reformulated instead of implemented (`AGENTS.md` §6): a fixture-based
      comparison at 299 K would evaluate `Cp°/R`, `H°/RT`, `S°/R`, `G°/RT` of an
      independent Python re-implementation against this node's functions and check they
      agree to 1e-12 — a proof that the *polynomials* agree, which `FunctionFixtureTests`
      already gives at every fixture temperature and needs no new point to hold at 299 K
      too. What the open bullet was really after — that `RecordLow`/`RecordHigh` pick
      the *reference's* bounds, not the old rule's, for exactly these eleven records —
      is proven directly and exactly by `RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds`,
      which reads the database's own bounds and needs no evaluation at any particular
      temperature to fail on the old rule: it already does, on all nine records the old
      rule got wrong. A 299 K fixture point would duplicate that proof one level removed
      (through `IsInRange`, at one more temperature) rather than add to it. The Thermo
      coder of 2026-09-28 made this call rather than touching the shared `tp`/`thermo`
      generator scripts outside its assignment.

      Evidence: `dotnet test tests/Thermo.Tests`, 1183/1183, `RangeQuestionTests` and
      `AssignedTemperatureCasesReproduceTheReference`'s below-300 K cases green; no
      `Bits*.approved.txt` differs from `main`.

- [x] 2026-09-27 — The threshold separates the committed file's transitions (the ⚠ of
      this date under Constraints).
      - `LatentHeatThresholdTests.NoSharedBoundFallsWithinAFactorTwoOfTheThreshold`
        scans every shared bound of the condensed product records (inside a record
        after the join, and between two records of one formula that concatenate),
        computing `|ΔH°/RT|` at each with `SpeciesFunctions.HOverRT(TemperatureInterval,
        double)`, the node's own function; it fails on an empty scan and asserts none
        falls within a factor 2 of `LatentHeatThreshold` on either side. Shown red at
        1e-3: `NaCN(II)` at 287.7 K (1.233e-3) and `NaCN(III)` at 290.4 K (8.470e-4) and
        293.15 K (1.336e-3) all land inside the old threshold's factor-2 band
        (`[0.5e-3, 2e-3]`); at 5e-3 the same scan is green (band `[2.5e-3, 1e-2]`).
      - `LatentHeatThresholdTests.TheCutFiresOnlyOnAlnAndSnS` generates the list of
        names whose scanned bounds reach the threshold and asserts it equals exactly
        `["ALN(L)", "SnS(cr)"]`; at 1e-3 the generated list also held `NaCN(II)` and
        `NaCN(III)`.
      - `LatentHeatThresholdTests.NaCnTwoAndNaCnThreeEachStayOnePiece` confirms both
        names are scanned (each has at least one internal bound) and that neither
        reaches the threshold, so each stays one piece; neither can be built alone
        through `SpeciesTable.Build` to check this the way `RangeQuestionTests` checks
        other cut names, since `NaCN(II)` alone has 6 intervals, over
        `TableLimits.MaxIntervalsPerSpecies`, independent of any threshold (confirmed
        by a scratch probe: `SpeciesTable.Build` throws "has 6 intervals, more than the
        limit of 5" regardless of `LatentHeatThreshold`), and no fixture holds either
        name.
      - Bits: no `Bits.approved.txt` or `Bits.linux.approved.txt` of any node moved
        (`git status` on all six files before and after, unchanged); the full
        `Thermo.Tests`, `Equilibrium.Tests`, `Performance.Tests` and `Problems.Tests`
        suites stay green (1177, 697, 700, 1186 respectively), confirming the
        Equilibrium pair rule (`CondensedSet.Pinnable`, the same constant) moves
        nothing either, since no committed fixture holds `NaCN`.

      Evidence: `dotnet test tests/Thermo.Tests`, 1177/1177 (three new facts); the
      three facts shown red once against the code of `9c33398` (threshold 1e-3) and
      green after `LatentHeatThreshold` was raised to 5e-3.

      ⚠ 2026-09-28: the third bullet's two reasons stopped holding the same day. The
      interval limit became 6, so `NaCN(II)` builds alone, and the sodium fixture lists
      both names. All three facts read the constant, so writing `>= 1.0e-3` in the
      builder in place of it left them green (the guards audit of 2026-09-28, finding
      F6). The criterion "The latent-heat cut is guarded where it happens" below closes
      this.

- [x] 2026-09-27 — The interval limit holds the committed file (Constraints). Evidence:
      - `IntervalLimitTests.NoProductNameExceedsTheIntervalLimitAfterTheJoin` (`tests/Thermo.Tests`)
        computes, from `Cpu.Database.Products`, the interval count of every product name
        joined the way `SpeciesResolution` joins it (one name's records concatenated,
        products only), asserts the largest is at most `TableLimits.MaxIntervalsPerSpecies`,
        fails on an empty scan, and was shown red at the limit of 5 (`NaCN(II) has 6
        intervals after the join, more than the limit of 5`); green at 6.
        `IntervalLimitTests.NaCnTwoIsTheOnlyRecordAtTheLimit` confirms `NaCN(II)` is the
        only name the scan finds at the limit, from the generated list. `dotnet test
        tests/Thermo.Tests`, 1180/1180 (`Category!=LongRunning`; three new facts).
      - NaNO3(a) with RP-1 builds a table and solves hp `Ok`: the fixtures node's new
        case (`cases/hp/nano3-rp1_of4_pc7MPa.json`, `propellants.py`'s `sodium_hp`,
        `regenerate.py --check` exits 0 over 328 fixtures) is covered automatically by
        `Equilibrium.Tests.AssignedEnthalpyCasesReproduceTheReference`'s directory
        listing, green (`dotnet test tests/Equilibrium.Tests`, 702/702), which builds
        the table (`NaCN(II)` among the candidate species), solves hp and compares
        every field with the fixture within the tolerance table.
      - The front-door leg (`Problems.Tests`) is green too, once the fixtures node's
        `role` field replaced the front-door's own guess: `PropellantTests.CandidateSpeciesEqualTheReferenceProductList`,
        `PropellantTests.ARatioSplitReproducesTheReferenceMassFractionsWithinItsSinglePrecision`,
        `EquilibriumTests.AssignedEnthalpyCasesReproduceTheReference` and
        `BitSnapshotTests.EveryFixtureGivesTheRecordedBits` all pass on the new case
        (`dotnet test tests/Problems.Tests --filter "Category!=LongRunning"`, 1191/1191;
        the fix and its own evidence are recorded in the fixtures node's and
        `Problems.Tests`' own `BOOT.md`, not repeated here per `AGENTS.md` §8's rule
        against retelling a foreign node's claim).
      - No bit snapshot moves apart from the new fixture's key in the three nodes that
        enumerate hp fixtures: `tests/Equilibrium.Tests/Bits.approved.txt`,
        `tests/Problems.Tests/Bits.approved.txt` and `tests/Thermo.Tests/Bits.approved.txt`
        each gained exactly one line (`hp/nano3-rp1_of4_pc7MPa.json`, `git diff --stat`
        on each: 1 insertion, 0 deletions). The Linux keys are recorded by the
        orchestrator under WSL.

        Corrected 2026-09-27 by the coder the same day: this item first said the thermo
        tests node's snapshot was unchanged, but its `BitSnapshotTests` enumerate every
        fixture case, hp included, and its new key was missing.

      ⚠ 2026-09-27: this criterion first stood partial, blocked on `Problems.Tests`'
      `FixtureCases.Oxidizers`, a hand-typed set of oxidizer reactant names that did
      not carry `NaNO3(a)`, found while adding the sodium case above and escalated to
      the orchestrator rather than patched by name (`AGENTS.md` §11: the fix touched a
      neighbour test node of neither `Thermo` nor `Fixtures`). The orchestrator's design
      gave every ratio case's reactant a recorded `role` in the fixture document itself,
      written by the generator from the oxidizer and fuel vectors it already builds,
      removing the guess rather than growing its name list (the fixtures node's `BOOT.md`
      and `Problems.Tests`' `BOOT.md` carry the design and the evidence).
- [x] 2026-09-27 — `KernelMath` (Constraints).
      - `Thermo.Tests.KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair`
        compares `KernelMath.Min` and `Max` with `System.Math.Min`/`Max` bit for bit,
        via the harness's `Bits.Same`, over every ordered pair of a domain holding ±0,
        ±∞, NaN, the smallest and largest subnormal, the smallest normal, eight
        ordinary values and a fixed 32-value sample spanning 30 decades on both signs
        (104² = 10 816 ordered pairs, both functions, fails on an empty domain). Shown
        red once: with the NaN branch of `KernelMath.Min` removed, the fact failed on
        every pair with one NaN operand — "Min(NaN, 2.2250738585072014E-308):
        Math.Min NaN, KernelMath.Min 2.2250738585072014E-308" among them — reverted,
        green again.
      - The execution node's probe runs `KernelMath.Min`/`Max` in place of
        `Math.Min`/`Max` (`Kernels.Probe`) and they equal the CPU accelerator on every
        input, NaN included: the execution tests node's criterion of the same date,
        `ProbeKernelTests.TheSpecialInputsAreRecordedAgainstCuda`, 0 ULP on every one
        of the 17 special inputs × 12 functions, `Min` and `Max` included.
      - The cost, measured on the reference machine (RTX 5070 Ti), the median of three
        `dotnet test tests/Execution.Tests -c Release --filter
        "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"` runs
        before the call-site change (`Math.Min`/`Max`, at `b3b9d5b`) and three after
        (`KernelMath.Min`/`Max`): CUDA 0.192 s → 0.189 s (1.6 % faster, not slower),
        CUDA kernel alone 0.118 s → 0.120 s, CPU accelerator 5.257 s → 4.409 s (the
        machine's other load varied between runs, `nvidia-smi` showing a second
        worktree's CUDA tests running concurrently during the noisiest sample,
        12.561 s). No `Throughput.approved.txt` or `Throughput.linux.approved.txt`
        was re-approved; the CUDA time did not regress, so nothing went to the owner.

      Evidence: `dotnet test tests/Thermo.Tests`, 1178/1178 (the new fact);
      `dotnet test tests/Execution.Tests -c Release` (no filter), 144/144 on CUDA —
      the 100 000-case sweep, the architecture fact over SM_75…SM_121, and the two
      probe facts included; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"`, 4547/4547, none skipped; the protocol lint 0 errors,
      0 warnings; no `Bits*.approved.txt` differs from `main` (`git status --short`
      names only the files this task touched, `Throughput*.approved.txt` excluded).

      ⚠ 2026-09-28: "they equal the CPU accelerator on every input, NaN included" held
      for the order the probe ran, a variable first and a constant second, and not for
      `Min(constant, NaN)` (the ⚠ of that date under Constraints). The criterion below
      carries the proof for both orders.
- [x] 2026-09-28 — `KernelMath` tests both operands for NaN before any ordered
      comparison (Constraints, 2026-09-28).
      - `KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair` stays
        green with the new form, over the same domain (two NaNs of different payloads
        added to it, so the first-operand payload rule is proven): `dotnet test
        tests/Thermo.Tests`, 1183/1183.
      - The execution node's probe gaining `KernelMath.Min(1.0, v)` and
        `KernelMath.Max(1.0, v)` on CUDA is the execution tests node's own criterion of
        the same date, not this one; this node's part is the form itself and its host
        proof above.
      - No `Bits*.approved.txt` moves: `git status --short tests/*/Bits*.approved.txt`
        empty. No in-tree call passes a NaN to either function, and the reordered form
        is algebraically the same function on every pair that reaches an ordered
        comparison (only the order of the NaN tests moved), so nothing on the CPU could
        move.
- [x] 2026-09-28 — The latent-heat cut is guarded where it happens, not at its constant
      (the second hidden-defect audit of 2026-09-28, guards finding F6).
      - `LatentHeatThresholdTests.TheBuilderCutsExactlyTheNamesTheScanPredicts` asks the
        builder directly: the names whose single-name table (`SpeciesTable.Build` over
        that name alone) has more than one piece, built and counted one at a time, equal
        the list the threshold scan generates. Shown red once with `>= 1.0e-3` written
        in `SpeciesResolution.Cut` in place of the constant: the builder then also cuts
        `NaCN(II)` and `NaCN(III)` while the scan (reading the unchanged constant) still
        expects only `ALN(L)` and `SnS(cr)` — exactly the gap the guards audit found,
        since every fact of 2026-09-27 reads the constant and stayed green under that
        same mutation.
      - `RangeQuestionTests.CondensedDatabaseRecordNames` no longer swallows an
        `ArgumentException` or a name the builder cuts unexpectedly: every condensed
        product name not in the threshold scan's own cut list is now required to build
        alone as exactly one piece, or the theory's discovery throws naming the piece
        count found. Shown red the same way (the mutation above turns `NaCN(II)` into
        an unexpected two-piece name, and `RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds`,
        which walks this list, fails with `'NaCN(II)' is not one of the scan's cut names
        but built as 2 pieces`).
      - `LatentHeatThresholdTests.NaCnTwoAndNaCnThreeEachStayOnePiece` is corrected: since
        the interval limit rose to 6 (BOOT.md, Constraints, the ⚠ of 2026-09-27) both
        names now build alone, and the sodium fixture's candidate list carries both (at
        zero moles). The fact now builds each alone through `SpeciesTable.Build` and
        asserts one piece, in addition to its existing scan-based checks.
      - `IsInRange`'s summary now says what the code does since 2026-09-26: the lowest
        lower and the highest upper bound of the species' intervals, taken bound by
        bound (the audit's O5).
      - The record-bounds criterion above is completed by a reformulation, with its own
        ⚠ (`AGENTS.md` §6): its remaining bullet asked for a generator change that a more
        direct, already-existing fact makes unnecessary.

      Evidence: all three facts shown red together at `5a732f0` with `>= 1.0e-3` in
      `SpeciesResolution.Cut` (`TheBuilderCutsExactlyTheNamesTheScanPredicts`,
      `NaCnTwoAndNaCnThreeEachStayOnePiece`, and
      `RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds` through the corrected
      `CondensedDatabaseRecordNames`), green again with the constant restored;
      `dotnet test tests/Thermo.Tests`, 1183/1183; no `Bits*.approved.txt` differs from
      `main`.
- [ ] The `KernelMath` host fact does not take `System.Math`'s NaN payload for an oracle
      (2026-09-30, the second CI run after the second audit, `windows-latest`, Release).
      - ⚠ 2026-09-30: `KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair`
        (the criteria of 2026-09-27 and 2026-09-28 above) compares `KernelMath` with
        `Math.Min` and `Math.Max` bit for bit, two NaNs of different payloads included, and
        this node's text says `KernelMath` "returns what .NET 10 returns for every pair,
        two NaNs included (the first operand's payload)". That holds for the managed body of
        `Math.Min` and `Math.Max`, which is what a Debug build runs. In optimized code RyuJIT
        expands both as hardware intrinsics, and for two NaNs of different payloads the
        expansion returns the other one. Measured on the reference machine (Ryzen 7 7800X3D,
        .NET SDK 10.0.112), the same fact: red in Release ("Min(NaN, NaN): Math.Min NaN,
        KernelMath.Min NaN", both NaN, bits different), also with `DOTNET_TieredCompilation=0`,
        `DOTNET_EnableAVX512F=0` and `DOTNET_EnableAVX10v1=0`; green in Debug and with
        `DOTNET_EnableHWIntrinsic=0`. One hosted Windows run passed it and the next failed
        it: the payload is not specified, and a hosted runner's CPU and JIT decide it.
        The fast suite of a Release build never ran on this machine before 2026-09-30,
        the reason no local run showed it.
      - **The claim.** `KernelMath.Min` and `Max` equal `Math.Min` and `Math.Max` on every
        pair whose `System.Math` result is not a NaN, bit for bit (±0 included), and return
        a NaN whenever `System.Math` does. For two NaNs, `KernelMath`'s own rule holds, and
        it is the one both accelerators share (the execution node's probe compares the two
        bit for bit): a pair with a NaN operand gives the first NaN operand, exactly its
        bits. The text above "returns what .NET 10 returns … two NaNs included" is
        corrected to say so.
      - **The fact.** Split in two, over the same domain: the equality of every non-NaN
        result and of NaN-ness against `System.Math`, and the payload rule asserted against
        the documented formula (`IsNaN(a) ? a : b` when a NaN is present), not against
        `System.Math`. Each shown red once (the NaN branch removed; the payload rule
        reversed to return the second NaN), green in Release and Debug and with
        `DOTNET_EnableHWIntrinsic=0`; both fail on an empty domain.

## Taboos

- No parsing of any file format: that is `Data`.
- No species selection, no propellant knowledge: indices in, values out.
- No `float`, no `LibDevice`, no ILGPU.Algorithms: the root forbids them in numerical nodes.
- No second physical constant, no atomic weight, no coefficient typed into code.
- No hidden ordering: the builder never sorts species or elements on its own.
