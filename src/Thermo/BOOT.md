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
  taken bound by bound (2026-09-26), the reference's rule (`minval(T_fit(:, 1))` and
  `maxval(T_fit(:, 2))`, cea 3.3.4). Interval selection itself is unchanged and is the
  reference's too, as `IntervalOf` does (`Si(cr)` at 299 K, measured on the package;
  H°/RT agrees to 5.7e-6, its older gas constant). → HISTORY.md#record-bounds-measured

  ⚠ 2026-09-26: was the record's bounds = first interval's low, last one's high, now
  the lowest low and highest high of its intervals → HISTORY.md#record-bounds

  ⚠ 2026-09-13: was `IsInRange` the condensed species' candidacy test, now the
  equilibrium node's effective bounds decide it → HISTORY.md#isinrange-candidacy
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

  ⚠ 2026-09-27: was at most 5 intervals per species, now 6 (`NaCN(II)` has six, so
  tables with Na, C and N were refused) → HISTORY.md#interval-limit-six
- **`KernelMath`** (2026-09-27, the root's math constraint): `Min(double, double)` and
  `Max(double, double)` for every numerical node. They return what `System.Math.Min`
  and `System.Math.Max` return for every pair whose `System.Math` result is not a NaN,
  signed zeros included, and a NaN whenever `System.Math` returns one; the payload of a
  NaN result is the rule below, not `System.Math`'s. They are written with comparisons
  and selections only: NaN if either operand is NaN, −0 below +0. Both operands are
  tested for NaN before any ordered comparison (2026-09-28):

  ```text
  Min(a, b) = IsNaN(a) ? a : IsNaN(b) ? b : a != b ? (a < b ? a : b) : (IsNegative(a) ? a : b)
  Max(a, b) = IsNaN(a) ? a : IsNaN(b) ? b : a != b ? (b < a ? a : b) : (IsNegative(b) ? a : b)
  ```

  A pair with a NaN operand gives the first NaN operand, exactly its bits: this is
  `KernelMath`'s own rule, the one both accelerators share, and no ordered comparison
  inside them ever sees a NaN, whichever side ILGPU moves a constant to (the root's third
  ILGPU defect). The names `double.IsNaN` and `double.IsNegative` are allowed inside
  `KernelMath`, and nowhere else in the numerical nodes, if ILGPU compiles them without
  libdevice.
  ⚠ 2026-09-30: was KernelMath returns what `Math.Min`/`Max` return for every pair,
  now the NaN payload is its own rule → HISTORY.md#kernelmath-payload

  ⚠ 2026-09-28: was NaN tested on the first operand only, as .NET does, now both
  operands before any ordered comparison → HISTORY.md#kernelmath-nan-first
- The join-and-cut threshold is `SpeciesFunctions.LatentHeatThreshold` = 5e-3 on
  `|ΔH°/RT|` at a shared bound (2026-09-27). It is the one constant separating a real
  latent heat from fit noise, and it lives here because the equilibrium node's pair
  rule tests the same quantity against the same constant. The committed file's scan
  puts the largest fit noise at 2.2e-3 (`NaCN(II)` → `NaCN(III)` at 288.5 K, a lambda
  transition) and the smallest real transition at 1.34e-2 (`BeO(a)` → `BeO(b)` at
  2373 K); 5e-3 sits at their geometric middle. → HISTORY.md#latent-heat-scan

  On the committed file the cut fires twice:
  - `ALN(L)`, whose two intervals differ by 68 kJ/mol at 2700 K;
  - the joined `SnS(cr)` at 875 K (|ΔH°/RT| 9.2e-2), where the file's two records of
    that name are the rhombic and the cubic phase.

  The concatenation covers every same-name record split of the file (`Co(b)`,
  `Cr(cr)`, `Cr2O3(I)` — three records — `Fe(a)`, `Fe2O3(cr)`, `Fe3O4(cr)`, `K2S(cr)`,
  `Na2S(cr)`, `Ni(cr)`, `SnS(cr)`), whose upper records were unreachable before (the
  database index returns the first record per name).

  ⚠ 2026-09-27: was threshold 1e-3, the cut firing once (`ALN(L)`), now 5e-3 and
  twice (`NaCN` no longer split) → HISTORY.md#latent-heat-threshold
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

  ⚠ 2026-09-12: was a `HostView` over the host arrays, now views only over accelerator
  buffers (ILGPU 1.5.3 views arrays in kernels only) → HISTORY.md#hostview

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

⚠ 2026-09-15: was Visibility public for `SpeciesTable` and `SpeciesFunctions`, now
internal, with other types (`API.md` lists them) → HISTORY.md#visibility-internal

Decisions taken with the reviews of 2026-09-14:

- **The table answers the range questions.** The contract carries
  `SpeciesTable.PieceOf(string species, double temperature)` (host side, the piece by
  the same rule as `IntervalOf`) and `SpeciesFunctions.RecordLow(in view, int)` and
  `RecordHigh(in view, int)` (the bounds `IsInRange` compares), so that no neighbour
  re-derives the layout (F-AR-01); `API.md` records them.
  → HISTORY.md#decision-range-questions
- **The duplicate-name records come from `Data`.** The builder takes the records of a
  name, in file order, from `SpeciesDatabase.Records` of `Data` and rebuilds no index.
  → HISTORY.md#decision-duplicate-records
- **The constructors of the view and the arrays are the declared exception** to the
  parameter rule: `SpeciesTableView` (11 parameters) and `SpeciesTableArrays` (8) are
  the layout itself, the aggregation mechanism the root names for kernels; eight of
  the eleven are caught by the type system on a swap, and splitting them into column
  structs would change the contract of four kernel nodes for no numerical gain. Every
  creation of the two names its arguments, as the root requires of a mirrored shape
  (added 2026-09-14: a scan of the construction sites found them positional).
- **The join compares the formation enthalpy too.** A same-name pair that disagrees in
  `FormationEnthalpy` is refused by name like a differing formula or molar mass
  (`SpeciesResolution.Joins`): the scan of `data/thermo.inp` (2 030 product records,
  ten repeated names) found none that disagrees, so the tests node exercises the
  refusal on a synthetic pair
  (`JoinAndCutTests.RecordsDisagreeingInFormationEnthalpyAreRefusedByName`).
  → HISTORY.md#decision-join-enthalpy
- **`MixtureMolarMass`** is one kilogram over the moles of all species, condensed
  included, in the code's summary as in `API.md` (F-TD-04). `SpeciesTableBuffers` stays
  here (F-TD-11). → HISTORY.md#decision-review-fixes
- **`SpeciesResolution`** (formerly `CondensedAssembly`; `Joins`, formerly `Touches`,
  2026-09-15) resolves every requested name, gaseous or condensed, not only the join
  and the cut: the name follows the scope. Ce (8) and every bit are unchanged.
  → HISTORY.md#decision-species-resolution
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

      ⚠ 2026-09-12: was the tolerance 0.1 % (the fit's own accuracy), now per species
      with the reason (sources are not JANAF) → HISTORY.md#crit-janaf-tolerance
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
      the root's code-shape constraint (`ShapeTests`; the two constructors declared
      above the only exceptions), the public surface grown only by `PieceOf`,
      `RecordLow` and `RecordHigh`, `PublicSurface.approved.txt` moved in the same
      commit, and every table bit for bit as at `8e36a27`: the tests node's bit snapshot
      unchanged (`dotnet test tests/Thermo.Tests`, 378 tests), `KernelEqualityTests` and
      the fixture tests green. → HISTORY.md#crit-decomposition
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
      tree names its arguments (the decision on the constructors): a scan of every
      `new T(…)` and `T x = new(…)` in `src/` and `tests/` found four sites, every
      argument named, the IL of the builds before and after identical, `Thermo.Tests`
      (378), `Equilibrium.Tests` (463) and `Transport.Tests` (157) green and
      `tests/Thermo.Tests/Bits.approved.txt` unchanged. The fact
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, designed that day,
      takes over as the evidence. → HISTORY.md#crit-named-arguments
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-28 — The record bounds are the reference's (the invariant "Interval
      selection is defined"): the equilibrium fixtures below 300 K (Si and Li in argon
      at 298.15 to 301 K, covered by
      `EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`) and
      `RangeQuestionTests.RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds` (every
      condensed record built alone, red against the old first/last rule on the nine
      anomaly records). Evidence: `dotnet test tests/Thermo.Tests`, 1183/1183; no
      `Bits*.approved.txt` differs from `main`. → HISTORY.md#crit-record-bounds

      ⚠ 2026-09-28: was a fourth proof, a 299 K fixture sample from a generator change,
      now dropped as duplicating the direct fact above → HISTORY.md#crit-record-bounds

- [x] 2026-09-27 — The threshold separates the committed file's transitions (the ⚠ of
      this date under Constraints).
      - `LatentHeatThresholdTests.NoSharedBoundFallsWithinAFactorTwoOfTheThreshold`
        scans every shared bound of the condensed product records with
        `SpeciesFunctions.HOverRT(TemperatureInterval, double)`, fails on an empty scan,
        and was red at 1e-3 (`NaCN(II)` at 287.7 K, `NaCN(III)` at 290.4 and 293.15 K).
      - `LatentHeatThresholdTests.TheCutFiresOnlyOnAlnAndSnS` generates the list of
        names whose bounds reach the threshold and asserts `["ALN(L)", "SnS(cr)"]`.
      - `LatentHeatThresholdTests.NaCnTwoAndNaCnThreeEachStayOnePiece`: both names are
        scanned and neither reaches the threshold.
      - No `Bits*.approved.txt` of any node moved; `Thermo.Tests`, `Equilibrium.Tests`,
        `Performance.Tests` and `Problems.Tests` green.
      Evidence: `dotnet test tests/Thermo.Tests`, 1177/1177; the three facts red once
      against `9c33398` (threshold 1e-3), green after the raise to 5e-3.
      → HISTORY.md#crit-latent-threshold

      ⚠ 2026-09-28: was the third bullet's reasons (limit 5, no fixture) held, now
      the limit is 6 → HISTORY.md#crit-latent-threshold-reasons

- [x] 2026-09-27 — The interval limit holds the committed file (Constraints). Evidence:
      - `IntervalLimitTests.NoProductNameExceedsTheIntervalLimitAfterTheJoin` computes
        the joined interval count of every product name, asserts the largest is at most
        `TableLimits.MaxIntervalsPerSpecies`, fails on an empty scan, and was red at the
        limit of 5; `IntervalLimitTests.NaCnTwoIsTheOnlyRecordAtTheLimit` names the only
        name at it; `dotnet test tests/Thermo.Tests`, 1180/1180.
      - NaNO3(a) with RP-1 builds a table and solves hp `Ok`: the fixtures node's case
        `cases/hp/nano3-rp1_of4_pc7MPa.json` through
        `Equilibrium.Tests.AssignedEnthalpyCasesReproduceTheReference`, 702/702.
      - The front-door leg is green (`Problems.Tests`, 1191/1191:
        `PropellantTests.CandidateSpeciesEqualTheReferenceProductList`,
        `EquilibriumTests.AssignedEnthalpyCasesReproduceTheReference`,
        `BitSnapshotTests.EveryFixtureGivesTheRecordedBits`); that node's `BOOT.md`
        holds it.
      - Only the new fixture's key moved, one line in each of the `Bits.approved.txt` of
        `Thermo.Tests`, `Equilibrium.Tests` and `Problems.Tests`.
      → HISTORY.md#crit-interval-limit

      ⚠ 2026-09-27: was partial, blocked on a hand-typed oxidizer set, now closed by
      the fixtures' `role` field → HISTORY.md#crit-interval-limit-partial
- [x] 2026-09-27 — `KernelMath` (Constraints).
      - `KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair`
        (`Thermo.Tests`) compared `KernelMath.Min` and `Max` with `System.Math` bit for
        bit over every ordered pair of a domain of 104 values (±0, ±∞, NaN, the
        subnormal bounds, 30 decades on both signs), failed on an empty domain and was
        red once with the NaN branch of `Min` removed.
      - The execution node's probe equals the CPU accelerator on every input, NaN
        included: `ProbeKernelTests.TheSpecialInputsAreRecordedAgainstCuda`, 0 ULP.
      - The cost: CUDA 0.192 s → 0.189 s on the throughput tripwire, nothing
        re-approved.
      Evidence: `dotnet test tests/Thermo.Tests`, 1178/1178; `dotnet test
      tests/Execution.Tests -c Release`, 144/144 on CUDA; the fast suite, 4547/4547; the
      protocol lint 0 and 0; no `Bits*.approved.txt` differs from `main`.
      → HISTORY.md#crit-kernelmath

      ⚠ 2026-09-28: was the probe equal on every input, now only variable first;
      `Min(constant, NaN)` is proved below → HISTORY.md#crit-kernelmath-order
- [x] 2026-09-28 — `KernelMath` tests both operands for NaN before any ordered
      comparison (Constraints, 2026-09-28):
      `KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair` stays
      green over the same domain with two NaNs of different payloads added (`dotnet
      test tests/Thermo.Tests`, 1183/1183); the probe with `KernelMath.Min(1.0, v)` on
      CUDA is the execution tests node's own criterion; no `Bits*.approved.txt` moves.
      → HISTORY.md#crit-kernelmath-nan-first
- [x] 2026-09-28 — The latent-heat cut is guarded where it happens, not at its constant
      (the second hidden-defect audit of 2026-09-28, guards finding F6).
      - `LatentHeatThresholdTests.TheBuilderCutsExactlyTheNamesTheScanPredicts` builds
        each name alone through `SpeciesTable.Build` and equals the scan's list; red
        once with `>= 1.0e-3` written in `SpeciesResolution.Cut`.
      - `RangeQuestionTests.CondensedDatabaseRecordNames` swallows no exception and no
        unexpected cut: every condensed name outside the scan's list builds alone as one
        piece.
      - `LatentHeatThresholdTests.NaCnTwoAndNaCnThreeEachStayOnePiece` builds each alone
        and asserts one piece (the limit rose to 6).
      Evidence: the three facts red together at `5a732f0` with the mutation, green with
      the constant restored; `dotnet test tests/Thermo.Tests`, 1183/1183; no
      `Bits*.approved.txt` differs from `main`. → HISTORY.md#crit-latent-cut-guarded
- [x] 2026-09-30 — The `KernelMath` host fact does not take `System.Math`'s NaN payload
      for an oracle (2026-09-30, the second CI run after the second audit,
      `windows-latest`, Release).
      - ⚠ 2026-09-30: was the fact `System.Math`-equal with NaN payloads, now the
        payload is `KernelMath`'s own rule → HISTORY.md#crit-kernelmath-payload
      - The claim: `KernelMath.Min` and `Max` equal `Math.Min` and `Math.Max` bit for
        bit on every pair whose `System.Math` result is not a NaN (±0 included) and
        return a NaN whenever it does; for two NaNs the first NaN operand, exactly its
        bits.
      - The fact, split over the same domain (104² ordered pairs):
        `KernelMathTests.MinAndMaxEqualSystemMathOnEveryNonNaNResultAndInNaNNessOverEveryOrderedPair`
        and `KernelMathTests.TwoNaNsGiveTheFirstNaNOperandExactly` (against
        `IsNaN(a) ? a : b`, two distinct NaN bit patterns required); each red once by
        mutating `KernelMath.cs`, both fail on an empty domain.
      Evidence: green in Release, in Debug and with `DOTNET_EnableHWIntrinsic=0`;
      `APTHERMO_NO_CUDA=1 dotnet test tests/Thermo.Tests`, 1193/1193 in Debug and in
      Release; red first in Release at `0c46d93`; no `Bits*.approved.txt` differs from
      `main`. The single fact named by the criteria of 2026-09-27 and 2026-09-28 is
      replaced by these two, their evidence dates stay.
      → HISTORY.md#crit-kernelmath-payload

## Taboos

- No parsing of any file format: that is `Data`.
- No species selection, no propellant knowledge: indices in, values out.
- No `float`, no `LibDevice`, no ILGPU.Algorithms: the root forbids them in numerical nodes.
- No second physical constant, no atomic weight, no coefficient typed into code.
- No hidden ordering: the builder never sorts species or elements on its own.
