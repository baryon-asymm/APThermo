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
  the same accelerator, and between the CPU accelerator and CUDA (2026-10-05: every
  function they call is a single IEEE operation or one of `KernelMath`'s correctly
  rounded `Exp`, `Log` and `Pow`, and the execution node's post-link forbids contraction).
  ⚠ 2026-10-05: was "agree within the math-function tolerance of the execution tests
  node", now bit-identical → HISTORY.md#bit-identical-2026-10-05
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
  `Max(double, double)` for every numerical node, and since 2026-10-05 `Exp`, `Log`, `Pow`
  and `Fma`: the tree's own correctly rounded elementary functions and its one fused
  multiply-add, each a single forwarding call to the child node
  [Elementary](Elementary/BOOT.md) (its algorithms, tables and facts). `Min` and `Max` return what `System.Math.Min`
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
- Math: only `KernelMath.Log` and `KernelMath.Pow` are needed (2026-10-05: not `System.Math`'s); `Pow`
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
| `Elementary/` | child node (2026-10-05, no project of its own): the correctly rounded exp, log and pow, their double-double and triple-double arithmetic, the exact midpoint cases of pow and the generated tables; reached through `KernelMath` only | internal |

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

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No parsing of any file format: that is `Data`.
- No species selection, no propellant knowledge: indices in, values out.
- No `float`, no `LibDevice`, no ILGPU.Algorithms: the root forbids them in numerical nodes.
- No second physical constant, no atomic weight, no coefficient typed into code.
- No hidden ordering: the builder never sorts species or elements on its own.
