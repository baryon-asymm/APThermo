# HISTORY.md — Thermo

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="bit-identical-2026-10-05"></a>

## 2026-10-05 — from "## Invariants" — the agreement between CPU and CUDA

Item 13 of release 0.2.2 (the tree's own correctly rounded `Exp`, `Log` and `Pow`, `src/Thermo/Elementary`) made the two accelerators equal bit for bit on every fixture family, the sweep and the species functions (`tests/Execution.Tests`). The text as it stood:

> Results are bit-identical between two calls with the same arguments on
> the same accelerator, and agree between the CPU accelerator and CUDA within the
> math-function tolerance of the execution tests node.

---

<a id="crit-kernelmath-payload"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the KernelMath host fact does not take System.Math's NaN payload for an oracle

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-30. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-30 — The `KernelMath` host fact does not take `System.Math`'s NaN payload for an oracle
>       (2026-09-30, the second CI run after the second audit, `windows-latest`, Release).
>       - ⚠ 2026-09-30: `KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair`
>         (the criteria of 2026-09-27 and 2026-09-28 above) compares `KernelMath` with
>         `Math.Min` and `Math.Max` bit for bit, two NaNs of different payloads included, and
>         this node's text says `KernelMath` "returns what .NET 10 returns for every pair,
>         two NaNs included (the first operand's payload)". That holds for the managed body of
>         `Math.Min` and `Math.Max`, which is what a Debug build runs. In optimized code RyuJIT
>         expands both as hardware intrinsics, and for two NaNs of different payloads the
>         expansion returns the other one. Measured on the reference machine (Ryzen 7 7800X3D,
>         .NET SDK 10.0.112), the same fact: red in Release ("Min(NaN, NaN): Math.Min NaN,
>         KernelMath.Min NaN", both NaN, bits different), also with `DOTNET_TieredCompilation=0`,
>         `DOTNET_EnableAVX512F=0` and `DOTNET_EnableAVX10v1=0`; green in Debug and with
>         `DOTNET_EnableHWIntrinsic=0`. One hosted Windows run passed it and the next failed
>         it: the payload is not specified, and a hosted runner's CPU and JIT decide it.
>         The fast suite of a Release build never ran on this machine before 2026-09-30,
>         the reason no local run showed it.
>       - **The claim.** `KernelMath.Min` and `Max` equal `Math.Min` and `Math.Max` on every
>         pair whose `System.Math` result is not a NaN, bit for bit (±0 included), and return
>         a NaN whenever `System.Math` does. For two NaNs, `KernelMath`'s own rule holds, and
>         it is the one both accelerators share (the execution node's probe compares the two
>         bit for bit): a pair with a NaN operand gives the first NaN operand, exactly its
>         bits. The text above "returns what .NET 10 returns … two NaNs included" is
>         corrected to say so.
>       - **The fact.** Split in two, over the same domain: the equality of every non-NaN
>         result and of NaN-ness against `System.Math`, and the payload rule asserted against
>         the documented formula (`IsNaN(a) ? a : b` when a NaN is present), not against
>         `System.Math`. Each shown red once (the NaN branch removed; the payload rule
>         reversed to return the second NaN), green in Release and Debug and with
>         `DOTNET_EnableHWIntrinsic=0`; both fail on an empty domain.
>
>       Evidence: `KernelMathTests.cs` now holds two facts over the unchanged domain (104²
>       ordered pairs, both functions):
>       - `MinAndMaxEqualSystemMathOnEveryNonNaNResultAndInNaNNessOverEveryOrderedPair`:
>         where `System.Math` returns a number `KernelMath` returns the same bits (`Bits.Same`,
>         ±0 included), where it returns a NaN `KernelMath` returns a NaN;
>       - `TwoNaNsGiveTheFirstNaNOperandExactly`: every pair with a NaN operand gives
>         `IsNaN(a) ? a : b` bit for bit; the fact also requires at least two distinct NaN bit
>         patterns in the domain, and a non-empty set of NaN pairs.
>
>       Reproduced first: `APTHERMO_NO_CUDA=1 dotnet test tests/Thermo.Tests --configuration
>       Release --filter "FullyQualifiedName~KernelMathTests"` red ("Min(NaN, NaN): Math.Min
>       NaN, KernelMath.Min NaN") at `0c46d93`. Shown red once each, `src/Thermo/KernelMath.cs`
>       mutated, reverted with `git checkout`, Release:
>       - the NaN branch of `KernelMath.Min` removed: both facts red (the first on every pair
>         with one NaN operand and a number, the second on the first-NaN rule);
>       - the payload rule reversed (`IsNaN(val2)` tested before `IsNaN(val1)` in `Min`): only
>         the payload fact red, the equality fact green, as designed.
>
>       Both facts fail on an empty domain (`Values()[..0]`: the equality fact on
>       `Assert.NotEmpty`, the payload fact on its distinct-NaN-pattern requirement). Green:
>       `KernelMathTests` 2/2 in Release, in Debug, and with `DOTNET_EnableHWIntrinsic=0` in
>       both configurations; `APTHERMO_NO_CUDA=1 dotnet test tests/Thermo.Tests` 1193/1193 in
>       Debug and 1193/1193 in Release. No `Bits*.approved.txt` differs from `main`.
>
>       The older criteria of 2026-09-27 and 2026-09-28 above name the single fact
>       `MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair`; it is replaced by the two
>       facts above, over the same domain, and their evidence dates stay: it was true of the
>       managed bodies a Debug build ran, which is the ⚠ of this criterion.

---

<a id="kernelmath-payload"></a>

## 2026-10-01 — from "## Constraints" — correction: KernelMath's NaN payload rule

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-30. What stays at the pointer is the current rule. The text as it stood:

>   inside them ever sees a NaN, whichever side ILGPU moves a constant to (the root's third
>   ILGPU defect).
>
>   ⚠ 2026-09-30: stood "This returns what .NET 10's managed `Math.Min` and `Math.Max`
>   return for every pair, and for two NaNs the first operand exactly" (and, before it,
>   "returns what `System.Math.Min` and `System.Math.Max` return for every pair of doubles,
>   NaN … included"). That holds for the managed bodies a Debug build runs. RyuJIT expands
>   both as hardware intrinsics in optimized code, and for two NaNs of different payloads
>   the expansion returns the other one: `System.Math` gives no payload guarantee. Found
>   by the second hosted Windows Release run after the second audit, reproduced on the
>   reference machine in Release; the criterion of 2026-09-30 below carries the evidence. The names
>   `double.IsNaN` and `double.IsNegative` are allowed inside `KernelMath`, and nowhere
>   else in the numerical nodes, if ILGPU compiles them without libdevice.

---

<a id="crit-latent-cut-guarded"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the latent-heat cut is guarded where it happens

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-28 — The latent-heat cut is guarded where it happens, not at its constant
>       (the second hidden-defect audit of 2026-09-28, guards finding F6).
>       - `LatentHeatThresholdTests.TheBuilderCutsExactlyTheNamesTheScanPredicts` asks the
>         builder directly: the names whose single-name table (`SpeciesTable.Build` over
>         that name alone) has more than one piece, built and counted one at a time, equal
>         the list the threshold scan generates. Shown red once with `>= 1.0e-3` written
>         in `SpeciesResolution.Cut` in place of the constant: the builder then also cuts
>         `NaCN(II)` and `NaCN(III)` while the scan (reading the unchanged constant) still
>         expects only `ALN(L)` and `SnS(cr)` — exactly the gap the guards audit found,
>         since every fact of 2026-09-27 reads the constant and stayed green under that
>         same mutation.
>       - `RangeQuestionTests.CondensedDatabaseRecordNames` no longer swallows an
>         `ArgumentException` or a name the builder cuts unexpectedly: every condensed
>         product name not in the threshold scan's own cut list is now required to build
>         alone as exactly one piece, or the theory's discovery throws naming the piece
>         count found. Shown red the same way (the mutation above turns `NaCN(II)` into
>         an unexpected two-piece name, and `RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds`,
>         which walks this list, fails with `'NaCN(II)' is not one of the scan's cut names
>         but built as 2 pieces`).
>       - `LatentHeatThresholdTests.NaCnTwoAndNaCnThreeEachStayOnePiece` is corrected: since
>         the interval limit rose to 6 (BOOT.md, Constraints, the ⚠ of 2026-09-27) both
>         names now build alone, and the sodium fixture's candidate list carries both (at
>         zero moles). The fact now builds each alone through `SpeciesTable.Build` and
>         asserts one piece, in addition to its existing scan-based checks.
>       - `IsInRange`'s summary now says what the code does since 2026-09-26: the lowest
>         lower and the highest upper bound of the species' intervals, taken bound by
>         bound (the audit's O5).
>       - The record-bounds criterion above is completed by a reformulation, with its own
>         ⚠ (`AGENTS.md` §6): its remaining bullet asked for a generator change that a more
>         direct, already-existing fact makes unnecessary.
>
>       Evidence: all three facts shown red together at `5a732f0` with `>= 1.0e-3` in
>       `SpeciesResolution.Cut` (`TheBuilderCutsExactlyTheNamesTheScanPredicts`,
>       `NaCnTwoAndNaCnThreeEachStayOnePiece`, and
>       `RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds` through the corrected
>       `CondensedDatabaseRecordNames`), green again with the constant restored;
>       `dotnet test tests/Thermo.Tests`, 1183/1183; no `Bits*.approved.txt` differs from
>       `main`.

---

<a id="crit-kernelmath-nan-first"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: KernelMath tests both operands for NaN first

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-28 — `KernelMath` tests both operands for NaN before any ordered
>       comparison (Constraints, 2026-09-28).
>       - `KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair` stays
>         green with the new form, over the same domain (two NaNs of different payloads
>         added to it, so the first-operand payload rule is proven): `dotnet test
>         tests/Thermo.Tests`, 1183/1183.
>       - The execution node's probe gaining `KernelMath.Min(1.0, v)` and
>         `KernelMath.Max(1.0, v)` on CUDA is the execution tests node's own criterion of
>         the same date, not this one; this node's part is the form itself and its host
>         proof above.
>       - No `Bits*.approved.txt` moves: `git status --short tests/*/Bits*.approved.txt`
>         empty. No in-tree call passes a NaN to either function, and the reordered form
>         is algebraically the same function on every pair that reaches an ordered
>         comparison (only the order of the NaN tests moved), so nothing on the CPU could
>         move.

---

<a id="crit-kernelmath-order"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: the probe held for one operand order only

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>       ⚠ 2026-09-28: "they equal the CPU accelerator on every input, NaN included" held
>       for the order the probe ran, a variable first and a constant second, and not for
>       `Min(constant, NaN)` (the ⚠ of that date under Constraints). The criterion below
>       carries the proof for both orders.

---

<a id="crit-latent-threshold-reasons"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: the third bullet's reasons

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>       ⚠ 2026-09-28: the third bullet's two reasons stopped holding the same day. The
>       interval limit became 6, so `NaCN(II)` builds alone, and the sodium fixture lists
>       both names. All three facts read the constant, so writing `>= 1.0e-3` in the
>       builder in place of it left them green (the guards audit of 2026-09-28, finding
>       F6). The criterion "The latent-heat cut is guarded where it happens" below closes
>       this.

---

<a id="crit-record-bounds"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the record bounds are the reference's, with its reformulation

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-28 — The record bounds are the reference's (the invariant "Interval
>       selection is defined" and its ⚠ of this date). Three of the four planned proofs
>       landed on 2026-09-26 already: the equilibrium fixtures below 300 K (Si and Li in
>       argon at 298.15, 299, 299.99, 300 and 301 K, covered by
>       `EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`'s directory
>       listing), `RangeQuestionTests.RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds`
>       (over every condensed record built alone, from a database listing, red against
>       the old first-interval/last-interval rule on the nine anomaly records), and no
>       bit snapshot moved.
>
>       ⚠ 2026-09-28: the remaining bullet asked for a generator change — a 299 K sample
>       for the eleven anomaly records — so that the `thermo` fixture comparison itself
>       exercised a point strictly between 298.15 and 300 K. The second hidden-defect
>       audit's O5 found this bullet still open with the code, the fixtures and the
>       generated-list fact it names already in place (the guards report of the same
>       date). Reformulated instead of implemented (`AGENTS.md` §6): a fixture-based
>       comparison at 299 K would evaluate `Cp°/R`, `H°/RT`, `S°/R`, `G°/RT` of an
>       independent Python re-implementation against this node's functions and check they
>       agree to 1e-12 — a proof that the *polynomials* agree, which `FunctionFixtureTests`
>       already gives at every fixture temperature and needs no new point to hold at 299 K
>       too. What the open bullet was really after — that `RecordLow`/`RecordHigh` pick
>       the *reference's* bounds, not the old rule's, for exactly these eleven records —
>       is proven directly and exactly by `RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds`,
>       which reads the database's own bounds and needs no evaluation at any particular
>       temperature to fail on the old rule: it already does, on all nine records the old
>       rule got wrong. A 299 K fixture point would duplicate that proof one level removed
>       (through `IsInRange`, at one more temperature) rather than add to it. The Thermo
>       coder of 2026-09-28 made this call rather than touching the shared `tp`/`thermo`
>       generator scripts outside its assignment.
>
>       Evidence: `dotnet test tests/Thermo.Tests`, 1183/1183, `RangeQuestionTests` and
>       `AssignedTemperatureCasesReproduceTheReference`'s below-300 K cases green; no
>       `Bits*.approved.txt` differs from `main`.

---

<a id="kernelmath-nan-first"></a>

## 2026-10-01 — from "## Constraints" — correction: KernelMath tests both operands for NaN first

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-28: stood "following the logic of .NET's own implementation … So both
>   accelerators run the same instructions". .NET's form tests only the first operand
>   for NaN and lets the ordered comparison `val1 < val2` decide a NaN second operand.
>   ILGPU 1.5.3 moves a constant left operand of a float comparison to the right and
>   inverts its NaN ordering while doing so (`IR/Construction/Compare.cs:67-85` and
>   `UpdateFlags` in `IR/Values/Compare.cs:128-140`: the toggle that is right for an
>   inversion is applied to a swap), so once inlining makes `val1` a constant,
>   `1.0 < v` compiles to `setp.gtu.f64` and `Min(1.0, NaN)` is 1.0 on CUDA and NaN on
>   the CPU. Run on the reference device: 18 mismatches over 12 outputs and 10 inputs,
>   all at the three NaN inputs. `Max` was safe in either order. The one production call
>   of that shape, `DampedStep`'s `Min(lambda, …)` with `lambda` = 1.0, cannot receive a
>   NaN (`largest > 0.0` guards it), so no result moved. The probe called only
>   `Min(v, 1.0)` and `Max(v, 1.0)`, variable first, and the host facts run where nothing
>   is swapped. Found by the second hidden-defect audit of 2026-09-28, independently by
>   its Execution part (finding F1, on the device and in ILGPU's source) and its guards
>   part (finding F2, by an offline PTX compile).

---

<a id="crit-kernelmath"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: KernelMath

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-27 — `KernelMath` (Constraints).
>       - `Thermo.Tests.KernelMathTests.MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair`
>         compares `KernelMath.Min` and `Max` with `System.Math.Min`/`Max` bit for bit,
>         via the harness's `Bits.Same`, over every ordered pair of a domain holding ±0,
>         ±∞, NaN, the smallest and largest subnormal, the smallest normal, eight
>         ordinary values and a fixed 32-value sample spanning 30 decades on both signs
>         (104² = 10 816 ordered pairs, both functions, fails on an empty domain). Shown
>         red once: with the NaN branch of `KernelMath.Min` removed, the fact failed on
>         every pair with one NaN operand — "Min(NaN, 2.2250738585072014E-308):
>         Math.Min NaN, KernelMath.Min 2.2250738585072014E-308" among them — reverted,
>         green again.
>       - The execution node's probe runs `KernelMath.Min`/`Max` in place of
>         `Math.Min`/`Max` (`Kernels.Probe`) and they equal the CPU accelerator on every
>         input, NaN included: the execution tests node's criterion of the same date,
>         `ProbeKernelTests.TheSpecialInputsAreRecordedAgainstCuda`, 0 ULP on every one
>         of the 17 special inputs × 12 functions, `Min` and `Max` included.
>       - The cost, measured on the reference machine (RTX 5070 Ti), the median of three
>         `dotnet test tests/Execution.Tests -c Release --filter
>         "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"` runs
>         before the call-site change (`Math.Min`/`Max`, at `b3b9d5b`) and three after
>         (`KernelMath.Min`/`Max`): CUDA 0.192 s → 0.189 s (1.6 % faster, not slower),
>         CUDA kernel alone 0.118 s → 0.120 s, CPU accelerator 5.257 s → 4.409 s (the
>         machine's other load varied between runs, `nvidia-smi` showing a second
>         worktree's CUDA tests running concurrently during the noisiest sample,
>         12.561 s). No `Throughput.approved.txt` or `Throughput.linux.approved.txt`
>         was re-approved; the CUDA time did not regress, so nothing went to the owner.
>
>       Evidence: `dotnet test tests/Thermo.Tests`, 1178/1178 (the new fact);
>       `dotnet test tests/Execution.Tests -c Release` (no filter), 144/144 on CUDA —
>       the 100 000-case sweep, the architecture fact over SM_75…SM_121, and the two
>       probe facts included; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
>       "Category!=LongRunning"`, 4547/4547, none skipped; the protocol lint 0 errors,
>       0 warnings; no `Bits*.approved.txt` differs from `main` (`git status --short`
>       names only the files this task touched, `Throughput*.approved.txt` excluded).

---

<a id="crit-interval-limit-partial"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: the interval-limit criterion first stood partial

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

>       ⚠ 2026-09-27: this criterion first stood partial, blocked on `Problems.Tests`'
>       `FixtureCases.Oxidizers`, a hand-typed set of oxidizer reactant names that did
>       not carry `NaNO3(a)`, found while adding the sodium case above and escalated to
>       the orchestrator rather than patched by name (`AGENTS.md` §11: the fix touched a
>       neighbour test node of neither `Thermo` nor `Fixtures`). The orchestrator's design
>       gave every ratio case's reactant a recorded `role` in the fixture document itself,
>       written by the generator from the oxidizer and fuel vectors it already builds,
>       removing the guess rather than growing its name list (the fixtures node's `BOOT.md`
>       and `Problems.Tests`' `BOOT.md` carry the design and the evidence).

---

<a id="crit-interval-limit"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the interval limit holds the committed file

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-27 — The interval limit holds the committed file (Constraints). Evidence:
>       - `IntervalLimitTests.NoProductNameExceedsTheIntervalLimitAfterTheJoin` (`tests/Thermo.Tests`)
>         computes, from `Cpu.Database.Products`, the interval count of every product name
>         joined the way `SpeciesResolution` joins it (one name's records concatenated,
>         products only), asserts the largest is at most `TableLimits.MaxIntervalsPerSpecies`,
>         fails on an empty scan, and was shown red at the limit of 5 (`NaCN(II) has 6
>         intervals after the join, more than the limit of 5`); green at 6.
>         `IntervalLimitTests.NaCnTwoIsTheOnlyRecordAtTheLimit` confirms `NaCN(II)` is the
>         only name the scan finds at the limit, from the generated list. `dotnet test
>         tests/Thermo.Tests`, 1180/1180 (`Category!=LongRunning`; three new facts).
>       - NaNO3(a) with RP-1 builds a table and solves hp `Ok`: the fixtures node's new
>         case (`cases/hp/nano3-rp1_of4_pc7MPa.json`, `propellants.py`'s `sodium_hp`,
>         `regenerate.py --check` exits 0 over 328 fixtures) is covered automatically by
>         `Equilibrium.Tests.AssignedEnthalpyCasesReproduceTheReference`'s directory
>         listing, green (`dotnet test tests/Equilibrium.Tests`, 702/702), which builds
>         the table (`NaCN(II)` among the candidate species), solves hp and compares
>         every field with the fixture within the tolerance table.
>       - The front-door leg (`Problems.Tests`) is green too, once the fixtures node's
>         `role` field replaced the front-door's own guess: `PropellantTests.CandidateSpeciesEqualTheReferenceProductList`,
>         `PropellantTests.ARatioSplitReproducesTheReferenceMassFractionsWithinItsSinglePrecision`,
>         `EquilibriumTests.AssignedEnthalpyCasesReproduceTheReference` and
>         `BitSnapshotTests.EveryFixtureGivesTheRecordedBits` all pass on the new case
>         (`dotnet test tests/Problems.Tests --filter "Category!=LongRunning"`, 1191/1191;
>         the fix and its own evidence are recorded in the fixtures node's and
>         `Problems.Tests`' own `BOOT.md`, not repeated here per `AGENTS.md` §8's rule
>         against retelling a foreign node's claim).
>       - No bit snapshot moves apart from the new fixture's key in the three nodes that
>         enumerate hp fixtures: `tests/Equilibrium.Tests/Bits.approved.txt`,
>         `tests/Problems.Tests/Bits.approved.txt` and `tests/Thermo.Tests/Bits.approved.txt`
>         each gained exactly one line (`hp/nano3-rp1_of4_pc7MPa.json`, `git diff --stat`
>         on each: 1 insertion, 0 deletions). The Linux keys are recorded by the
>         orchestrator under WSL.
>
>         Corrected 2026-09-27 by the coder the same day: this item first said the thermo
>         tests node's snapshot was unchanged, but its `BitSnapshotTests` enumerate every
>         fixture case, hp included, and its new key was missing.

---

<a id="crit-latent-threshold"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the threshold separates the committed file's transitions

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-27 — The threshold separates the committed file's transitions (the ⚠ of
>       this date under Constraints).
>       - `LatentHeatThresholdTests.NoSharedBoundFallsWithinAFactorTwoOfTheThreshold`
>         scans every shared bound of the condensed product records (inside a record
>         after the join, and between two records of one formula that concatenate),
>         computing `|ΔH°/RT|` at each with `SpeciesFunctions.HOverRT(TemperatureInterval,
>         double)`, the node's own function; it fails on an empty scan and asserts none
>         falls within a factor 2 of `LatentHeatThreshold` on either side. Shown red at
>         1e-3: `NaCN(II)` at 287.7 K (1.233e-3) and `NaCN(III)` at 290.4 K (8.470e-4) and
>         293.15 K (1.336e-3) all land inside the old threshold's factor-2 band
>         (`[0.5e-3, 2e-3]`); at 5e-3 the same scan is green (band `[2.5e-3, 1e-2]`).
>       - `LatentHeatThresholdTests.TheCutFiresOnlyOnAlnAndSnS` generates the list of
>         names whose scanned bounds reach the threshold and asserts it equals exactly
>         `["ALN(L)", "SnS(cr)"]`; at 1e-3 the generated list also held `NaCN(II)` and
>         `NaCN(III)`.
>       - `LatentHeatThresholdTests.NaCnTwoAndNaCnThreeEachStayOnePiece` confirms both
>         names are scanned (each has at least one internal bound) and that neither
>         reaches the threshold, so each stays one piece; neither can be built alone
>         through `SpeciesTable.Build` to check this the way `RangeQuestionTests` checks
>         other cut names, since `NaCN(II)` alone has 6 intervals, over
>         `TableLimits.MaxIntervalsPerSpecies`, independent of any threshold (confirmed
>         by a scratch probe: `SpeciesTable.Build` throws "has 6 intervals, more than the
>         limit of 5" regardless of `LatentHeatThreshold`), and no fixture holds either
>         name.
>       - Bits: no `Bits.approved.txt` or `Bits.linux.approved.txt` of any node moved
>         (`git status` on all six files before and after, unchanged); the full
>         `Thermo.Tests`, `Equilibrium.Tests`, `Performance.Tests` and `Problems.Tests`
>         suites stay green (1177, 697, 700, 1186 respectively), confirming the
>         Equilibrium pair rule (`CondensedSet.Pinnable`, the same constant) moves
>         nothing either, since no committed fixture holds `NaCN`.
>
>       Evidence: `dotnet test tests/Thermo.Tests`, 1177/1177 (three new facts); the
>       three facts shown red once against the code of `9c33398` (threshold 1e-3) and
>       green after `LatentHeatThreshold` was raised to 5e-3.

---

<a id="latent-heat-threshold"></a>

## 2026-10-01 — from "## Constraints" — correction: the threshold 1e-3 to 5e-3

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-27: stood "= 1e-3 … the smallest real transition of the committed file is
>   BeO a/b at 1.34e-2, the largest interval-split artifact 3.9e-4 (`Cr(cr)`) … On the
>   committed file the cut fires exactly once — `ALN(L)` … the only such jump among the
>   203 multi-interval condensed product records". The scan of 2026-09-13 missed `NaCN`
>   and did not count the bound a join creates. With 1e-3 the cut also fired at
>   `NaCN(II)` 287.7 K and `NaCN(III)` 293.15 K, splitting each into pieces with a
>   phantom latent heat of about 3 J/mol, and the pair rule pinned `NaCN(II)`/`NaCN(III)`
>   at 288.5 K as a melting plateau. The reference treats each record as one species.
>   The `SnS(cr)` cut is real and stays. Found by the Equilibrium coder of 2026-09-26,
>   whose many-phase fixture split `SnS(cr)` and `NaCN(III)`; the orchestrator's scan
>   confirmed it with the generator's own reader.

---

<a id="latent-heat-scan"></a>

## 2026-10-01 — from "## Constraints" — the latent-heat threshold: the scan of the committed file

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

>   `|ΔH°/RT|` at a shared bound (2026-09-27). It is the one constant separating a real
>   latent heat from fit noise, and it lives here because the equilibrium node's pair
>   rule tests the same quantity against the same constant. The committed file, scanned
>   2026-09-27 over every shared bound of the condensed product records, inside a record
>   and between two records of one formula:
>   - the largest fit noise is 2.2e-3, `NaCN(II)` → `NaCN(III)` at 288.5 K, a lambda
>     transition, which has no latent heat; inside a record the largest is 1.34e-3,
>     `NaCN(III)` at 293.15 K;
>   - the smallest real transition is 1.34e-2, `BeO(a)` → `BeO(b)` at 2373 K;
>   - nothing lies between the two, and 5e-3 sits at their geometric middle.

---

<a id="interval-limit-six"></a>

## 2026-10-01 — from "## Constraints" — correction: the interval limit 5 to 6

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-27: stood "at most 5 intervals per species". `NaCN(II)` has six intervals
>   in one record, the only such product record of the committed file, so every table
>   whose elements include Na, C and N was refused. `apthermo equilibrium` on NaNO3(a) and
>   RP-1 at O/F 4, 7 MPa, hp, printed "species 'NaCN(II)' has 6 intervals, more than the
>   limit of 5" at `1dfc44e`, and the front door let the builder's `ArgumentException`
>   through. Found by the Thermo coder of 2026-09-27 while testing the latent-heat
>   threshold; the orchestrator's scan with the generator's reader confirmed that no
>   other record exceeds 5.

---

<a id="record-bounds"></a>

## 2026-10-01 — from "## Invariants" — correction: the record's bounds

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-26: `RecordLow` was "the first interval's lower bound" and `RecordHigh` "the
>   last interval's upper bound", which assumes a record's intervals ascend. Eleven
>   condensed records of the committed file begin with an inverted interval (the Data
>   node's anomaly list).
>   - Nine run 300 → 298.15 before a regular interval from 298.15 K (`Ca(a)`, `CrN(cr)`,
>     `FeCL3(cr)`, `FeOCL(cr)`, `Fe3O4(cr)`, `Li(cr)`, `NH4F(cr)`, `Si(cr)`,
>     `Ti3O5(a)`). The old rule refused them between 298.15 and 300 K, where their data
>     hold and the reference admits them.
>   - A tp of Si in argon at 299 K returned `Ok` with Si3 vapour (253 kJ/kg) where cea
>     3.3.4 has `Si(cr)` (0 kJ/kg). The two agree to the last printed digit from 300 K on.
>   - For `Br2(cr)` (one interval, 300 → 265.9) and `U3O8(II)` (300 → 300, then
>     300 → 483) the two rules agree: `Br2(cr)` is in range nowhere, in the reference
>     too.
>
>   Found by the hidden-defect audit of 2026-09-26 (Thermo and Equilibrium, finding 1),
>   confirmed against cea 3.3.4 the same day. The tests missed it for two reasons: the
>   fixture generator used the same first-bound rule, and no fixture species had an
>   inverted interval.

---

<a id="record-bounds-measured"></a>

## 2026-10-01 — from "## Invariants" — interval selection: how the reference's bounds were measured

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>   taken bound by bound (2026-09-26). This is the reference's rule
>   (`minval(T_fit(:, 1))` and `maxval(T_fit(:, 2))`, cea 3.3.4 `equilibrium.f90`
>   1692–1693 and 1913–1915). Interval selection itself is unchanged. It is the
>   reference's selection too: for `Si(cr)` cea 3.3.4 evaluates the inverted first piece
>   at 298.15 K and the second interval at 299 K and 300 K, as `IntervalOf` does.
>   Measured 2026-09-26 through the package's `calc_property`. It agrees to 5.7e-6
>   relative in H°/RT, which is the package's older gas constant 8.31451.

---

<a id="decision-species-resolution"></a>

## 2026-10-01 — from "## Structure" — decision: CondensedAssembly renamed SpeciesResolution

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> - **`CondensedAssembly` is renamed `SpeciesResolution`, `Touches` renamed `Joins`**
>   (2026-09-15, the clean-code repair's R-Thermo-1). The type's own summary and its
>   Structure row above described it as only "the join … and the cut", but the code has
>   always resolved every requested name through it, gaseous or condensed: the gas
>   branch, the reactant-record fallback for a name no product carries, and the
>   stoichiometry check that refuses a foreign element sit beside the join-and-cut,
>   named by neither. The name now matches the scope instead of the scope being cut
>   back to the name: Ce and every bit are unchanged (`SpeciesResolution` measures Ce 8,
>   as `CondensedAssembly` did), only the identifiers and the two descriptions move.
>   `SpeciesTable.cs`'s two references and `Thermo.Tests`' one doc-comment mention
>   renamed with it.

---

<a id="visibility-internal"></a>

## 2026-10-01 — from "## Structure" — correction: SpeciesTable and SpeciesFunctions are internal

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-15 (distribution phase): the Visibility column read "public" for `SpeciesTable`
> and `SpeciesFunctions`. The API review of that day (fixed in `e284939`) found
> no consumer scenario for either: every use is a neighbour numerical node composing the
> kernel layer, or this node's own tests. Both, with `PhysicalConstants`,
> `SpeciesTableArrays`, `SpeciesTableBuffers`, `SpeciesTableView` and `TableLimits`, became
> `internal`, with `InternalsVisibleTo` grants to the nodes that use them
> (`APThermo.Thermo.csproj`; `API.md`'s tree-contract sections list them). `MixtureState`
> and `CaseStatus` stay public: a consumer reads them from the result records of `Problems`.

---

<a id="crit-named-arguments"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: every creation of the wide constructors names its arguments

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — Every creation of `SpeciesTableView` and `SpeciesTableArrays` in the
>       tree names its arguments (the decision on the constructors of the view and the
>       arrays), the protocol tests node's named-construction fact green once it exists;
>       the tests node's bit snapshot unchanged. A scan of every `new T(…)` and
>       `T x = new(…)` of the two names in `src/` and `tests/` (a script outside the tree)
>       finds four sites, in `SpeciesTableView`, `TableLayout`, `Equilibrium.Tests`'
>       `InvalidInputTests` and `Transport.Tests`' `StatusTests`, every argument named;
>       the builds of `Thermo`, `Equilibrium.Tests` and `Transport.Tests` after the change
>       carry the IL of the builds before it, method by method, so no argument binds to
>       another parameter; `Thermo.Tests` (378), `Equilibrium.Tests` (463) and
>       `Transport.Tests` (157) green; `tests/Thermo.Tests/Bits.approved.txt` unchanged
>       (blob `8bd5068e` before and after). The fact,
>       `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
>       and not yet written; it takes over as the evidence when it is.

---

<a id="crit-decomposition"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the decomposition of the Structure

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — The decomposition of `## Structure`: every type of the node within
>       the root's code-shape constraint (measured by hand pending the protocol tests
>       node's `ShapeTests`, root `BOOT.md`; the largest new file, `SpeciesTable.cs`,
>       179 lines; the two constructors declared above the only exceptions), the public
>       surface grown only by `PieceOf`, `RecordLow` and `RecordHigh` (with `Records` of
>       the `Data` node, the snapshot moves by exactly four lines over the whole task),
>       `PublicSurface.approved.txt` moved in the same commit, and every table bit for
>       bit as at `8e36a27`: the tests node's bit snapshot over every fixture case's
>       table unchanged (`dotnet test tests/Thermo.Tests`, 378 tests), `KernelEqualityTests`
>       and the fixture tests green, the fast suite green.

---

<a id="decision-review-fixes"></a>

## 2026-10-01 — from "## Structure" — decisions of the reviews: the summary of MixtureMolarMass, SpeciesTableBuffers

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - **`MixtureMolarMass`'s summary in the code** says what `API.md` has said since
>   2026-09-12: one kilogram over the moles of all species, condensed included (the
>   review's F-TD-04: the rename of that day changed the field and the document and
>   left the comment).
> - **`SpeciesTableBuffers` stays here**; the "out of scope" line of `API.md` that
>   contradicted it goes (the review's F-TD-11).

---

<a id="decision-join-enthalpy"></a>

## 2026-10-01 — from "## Structure" — decision of the reviews: the join compares the formation enthalpy

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - **The join compares the formation enthalpy too**, if the committed file lets it:
>   the same-name product groups are scanned first; where none disagrees, a disagreeing
>   pair is refused like a differing formula or molar mass; where one does, the rule is
>   recorded here instead and the first record's value stands (the review's F-TD-09).
>
>   Confirmed 2026-09-14 by a scan of `data/thermo.inp` (2 030 product records; the
>   scan's own count matches `ThermoLoadTests.EveryRecordOfTheFileIsParsed`):
>   ten names repeat in the PRODUCTS section — `Co(b)`, `Cr(cr)`, `Cr2O3(I)`, `Fe(a)`,
>   `Fe2O3(cr)`, `Fe3O4(cr)`, `K2S(cr)`, `Na2S(cr)`, `Ni(cr)`, `SnS(cr)`, the same ten
>   the concatenation list above already named — and none disagrees in
>   `FormationEnthalpy`. The join therefore refuses a disagreeing pair exactly as it
>   refuses a differing formula or molar mass (`SpeciesResolution.Joins`); the tests
>   node exercises the refusal on a synthetic pair, since no real one disagrees
>   (`JoinAndCutTests.RecordsDisagreeingInFormationEnthalpyAreRefusedByName`).

---

<a id="decision-duplicate-records"></a>

## 2026-10-01 — from "## Structure" — decision of the reviews: duplicate-name records come from Data

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - **The duplicate-name records come from `Data`.** The builder no longer rebuilds a
>   name → records index over the whole product list on every call: `Data` publishes
>   the records of a name in file order (`SpeciesDatabase.Records`, its own decision of
>   the same day), and the sentence under Constraints about the database index
>   returning the first record per name now points at the neighbour's contract
>   instead of restating it.

---

<a id="decision-range-questions"></a>

## 2026-10-01 — from "## Structure" — decision of the reviews: the table answers the range questions

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - **The table answers the range questions.** Two neighbours re-derived this node's
>   interval layout: the front door found the piece of a cut record covering a
>   temperature, and the equilibrium solver read a record's first lower and last upper
>   bound from the arrays (the architecture review's F-AR-01). The contract gains
>   `SpeciesTable.PieceOf(string species, double temperature)` (host side, the piece by
>   the same rule as `IntervalOf`) and `SpeciesFunctions.RecordLow(in view, int)` and
>   `RecordHigh(in view, int)` (kernel-compatible, the bounds `IsInRange` compares);
>   they evaluate the identical expressions, so nothing moves. The neighbours switch to
>   them in their own tasks. Recorded in `API.md` with its ⚠; the snapshot moves in the
>   same commit.

---

<a id="isinrange-candidacy"></a>

## 2026-10-01 — from "## Invariants" — correction: IsInRange as the condensed candidacy test

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-13. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-13: stood "for condensed species `IsInRange` is the candidacy test other
>   nodes rely on". The melting-plateau analysis of this date moved the equilibrium
>   node's condensed candidacy to effective bounds — the crossing of adjacent records'
>   Gibbs curves, which that node derives from this table's bounds and fits, because
>   the committed fits cross up to 2.7e-3 K away from the printed bound and a pinned
>   two-phase pair is exempt from any range test. `IsInRange` itself is unchanged.

---

<a id="crit-janaf-tolerance"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: the JANAF tolerance

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

>       ⚠ 2026-09-12: stood "within the fit accuracy stated by the NASA report: 0.1 %".
>       That figure is the fit's accuracy against its own source data, and the sources of
>       these four records are Gurvich et al. (`H2`, `N2`, `CO2`) and Woolley 1987 (`H2O`),
>       not JANAF; the compilations differ from JANAF by up to 0.12 % (`CO2` at 3000 K)
>       and 1.9 % (`H2O` at 3000 K, where Woolley's partition function supersedes the 1985
>       table). The JANAF comparison is a plausibility check of formulas and units; the
>       correctness check is the 1e-12 comparison above. Found when the test first ran.

---

<a id="hostview"></a>

## 2026-10-01 — from "## Constraints" — correction: no HostView over arrays

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-12: stood "on the host, the same layout is exposed as arrays so that tests
>   and the builder need no accelerator", with a `HostView` over the arrays in `API.md`.
>   ILGPU 1.5.3 converts a managed array into a view only inside kernels
>   (`ArrayViewExtensions.AsArrayView`: "supported in kernels only"); outside a kernel a
>   view needs a memory buffer of an accelerator. The builder still needs none; the
>   tests create the CPU accelerator to evaluate. Found when the view was implemented.

---
