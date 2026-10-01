# HISTORY.md — Problems

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="criterion-third-pass-0928"></a>

## 2026-10-01 — from "## Acceptance criteria", "the third audit pass" — condensed wording, with the pre-fix status and the evidence figures

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-28 — The third audit pass of 2026-09-28 (part 2, finding 3) is closed: the
>       refusal of an element with no candidate species does not depend on what the
>       solver solved before. `ChemicalSystemCache.Get(IReadOnlyList<string>, …)` now
>       calls `ValidatedCandidates` (which reads the call's own `elementsWithAbundance`)
>       before the cache's `_systems.TryGetValue` lookup, not after, so every call
>       validates its own mixture's abundances whether or not the underlying
>       `ChemicalSystem` is already cached under the same (elements, `Omit`, `Only`) key.
>       `Union` and the single-mixture `Solve` overloads (which, unlike `SolveStates`/
>       `SolveRocketStates`, never called `ValidateOwnElements`) go through the same
>       `Get`, so the fix covers every caller from one place.
>
>       Fact: `Problems.Tests/ThirdPassFixTests.TheNoCandidateRefusalDoesNotDependOnTheCachesHistory`
>       — a fresh `Solver` refuses a mixture with `"E": 1.0e-6` (control); the same
>       `Solver`, having first solved the same elements with `"E": 0.0` (masked, not
>       refused, `SecondAuditFixTests`'s finding F1), still refuses the positive-`E`
>       mixture with the same message. Red at `c02e14d`: the second call was not refused
>       at all (`Assert.Throws` failed, "No exception was thrown"; the audit's own probe,
>       `scratchpad/audit3/b/cache.txt`, records the pre-fix status as `SingularMatrix`);
>       green after moving the validation before the lookup.
>
>       Evidence: `dotnet build APThermo.sln`, 0 warnings, 0 errors; `dotnet test
>       tests/Problems.Tests`, 1252 of 1252, none skipped; `APTHERMO_NO_CUDA=1 dotnet test
>       APThermo.sln --filter "Category!=LongRunning"`, every project green; the protocol
>       lint, 0 errors, 0 warnings; no `Bits*.approved.txt` or `PublicSurface.approved.txt`
>       moved.

---

<a id="criterion-audit-0928"></a>

## 2026-10-01 — from "## Acceptance criteria", "the audit fixes of 2026-09-28" — condensed wording, with the red-once method and the evidence figures

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-28 — The audit fixes of 2026-09-28 (Constraints). Each fact is red once
>       against `5a732f0` (`git checkout 5a732f0 -- src/Problems`, the new fact run and
>       seen red, then `git checkout HEAD -- src/Problems` to restore), then green,
>       in `tests/Problems.Tests/SecondAuditFixTests.cs`:
>       - `AnElementAtZeroAbundanceEverywhereIsMaskedNotRefused`: a record with
>         `"E": 0.0` beside the approved AP/Al record solves, and its station fields
>         equal the record's without `E` bit for bit; a record with `"e": 1e-6` is
>         still refused;
>       - `AnOnlyListNamesItsOwnCauseAndExcludesIonsAndInertRecords`:
>         `"only": ["H2", "H"]` on LOX/LH2 is refused, naming the `only` list as the
>         cause; `e-`, `H+` and `InertH` in `only` are each refused by name;
>       - `OneAmountKindPerUnitOfNormalizationAppliesAcrossGroupsOnlyWithoutARatio`:
>         without a ratio, a fuel in mass fractions beside a named reactant in moles is
>         refused, and so is an oxidizer in moles beside a named reactant in mass
>         fractions; with a ratio, the per-group rule of 2026-09-26 is unchanged;
>       - `ARecordIsNamedForItsOwnElementsABatchForItsOptions`: in a batch of three
>         records, one with an unknown element and one with `"e": 1e-6`, each is a
>         `StateRecordException` with its own index. A transport request against a
>         database without `trans.inp` is an `ArgumentException` naming the option
>         (not a `StateRecordException`), and so is `StateBatchOptions.MassTolerance`
>         NaN;
>       - `EstimatesMustBeFinite`: a rocket `TemperatureEstimate` of +∞ and an
>         equilibrium `Temperature` of +∞ are refused;
>       - `AmountsMustSumToAFiniteValue`: two fuels of 1e308 are refused naming the
>         fuel group's non-finite sum;
>       - `Br2ResolvesAt298Point15KAgainstTheGeneratedReference`: `Br2(cr)` at
>         298.15 K resolves, its enthalpy equal to cea 3.3.4's own
>         (−68 567.575148 J/kg, `tests/Fixtures/cases/reactant/Br2_cr__298.15K.json`,
>         generated by `propellants.py`'s `br2_reactant_anomaly`, never typed);
>       - `TwoOmitListsThatJoinToTheSameTextGiveTwoTables`: two `omit` lists that used
>         to join to the same key now give two tables, each excluding only the species
>         it named.
>
>       `API.md`'s errors table states every refusal above. No `Bits*.approved.txt` or
>       `PublicSurface.approved.txt` moved (`git status --short`, unchanged from `main`).
>
>       Evidence: `dotnet test tests/Problems.Tests/APThermo.Problems.Tests.csproj`:
>       1206 of 1206, none skipped (1197 baseline, plus the 8 facts above and the
>       reacting-fields pinning fact of `tests/Problems.Tests/BOOT.md`, 2026-09-28). The
>       protocol lint: 0 errors, 0 warnings.

---

<a id="criterion-audit-0926-one-sided"></a>

## 2026-10-01 — from "## Acceptance criteria", "the audit fixes of 2026-09-26" — three facts that tried one side of their rule

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>       ⚠ 2026-09-28: three of these facts tried only one side of their rule. The
>       no-candidate fact gave the element positive moles only, the amount-kind fact
>       mixed kinds inside one group and only with a ratio, and the record-index fact
>       covered the rules of `ProblemValidation` but not the elements of a record. The
>       second audit found a regression and two gaps behind them (the ⚠ notes of
>       2026-09-28 under Constraints).

---

<a id="shape-solver-ce-24"></a>

## 2026-10-01 — from "## Shape exceptions" — Solver's efferent coupling 22 to 24

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-28: `Solver`'s row stood at 22. The audit fixes of 2026-09-28 add
> `ValidatedOptions` (naming `ElementalMixture` in its mass-tolerance check) and
> `ValidateRecordElements` (naming `StateRecordException` and looping the records
> `ChemicalSystemCache.ValidateOwnElements` now validates per record, before the union),
> both composition-root code of `Solver` itself, no formula; the protocol tests node's
> `ShapeTests` measures 24 on the walk. `Solver` remains the composition root the reason
> column already describes; the two new methods hold no rule of their own, only the
> record-vs-batch dispatch finding F3 asked for.

---

<a id="audit-0928-overflow-example"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-28, amounts sum to a finite value" — the example of the overflowing sum

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>     value is refused, naming the group (observation 7: two fuels of 1e308 were
>     refused by the mass check as if the oxidizer alone weighed the kilogram).

---

<a id="audit-0928-record-or-batch"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-28, a record and a batch" — the elements checked on the union

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>     - ⚠ The rule of 2026-09-26 above says the record's exception covers "an element
>       the database lacks". The code checked the elements on the union, where no index
>       exists, and threw a plain `ArgumentException`. The batch-level conditions went the
>       other way and were blamed on record 0 (`states.jsonl:1: transport properties were
>       requested …`). In a JSON Lines file of 100 000 records the first left no way to
>       find the record (finding F3).

---

<a id="audit-0928-amount-kinds"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-28, one amount kind per unit of normalization" — the per-group check without a ratio

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>     - ⚠ The rule of 2026-09-26 checked each group alone. Without a ratio, a fuel group
>       in mass fractions beside a named group in moles passed both checks and was pooled:
>       `H2(L)` 0.5 beside 0.5 mol of `CH4(L)` made `CH4(L)` 94.13 % of the mass, the
>       first audit's own figure (finding F2).

---

<a id="audit-0928-only-example"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-28, an Only list names candidates only" — the example of the ions solved through Only

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>     rule above excludes them. Observation 2: `e-`, `H+`, `OH-`, `H3O+` or `InertH` in
>     `only` were solved, against that rule.

---

<a id="audit-0928-refusal-cause-example"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-28, the refusal names its real cause" — the example of the refusal's wrong cause

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>     case. Observation 1: `"only": ["H2", "H"]` on LOX/LH2 was refused as "element 'O'
>     has no candidate species: only ionized or inert records carry it".

---

<a id="audit-0928-zero-abundance"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-28, an element without candidates" — the regression of the element-list rule

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>     - ⚠ The rule of 2026-09-26 looked at the element list, never at the abundances.
>       A state record that lists `"E": 0.0`, as a plasma-capable code writes a neutral
>       mixture, solved at `9c33398` bit for bit like the record without it, and was
>       refused at `5a732f0` (finding F1, a regression of that fix).

---

<a id="criterion-audit-0926"></a>

## 2026-10-01 — from "## Acceptance criteria", "the audit fixes of 2026-09-26" — condensed wording, with the evidence due and the red-once narrative

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-26 — The audit fixes of that date (Constraints). Evidence due, each fact
>       red once against the code of `9c33398`:
>       - a batch of three records split by exits, the third with pressure 0, refused
>         with `Index` 2;
>       - an `InertH2(L)` fuel, and a record with `E` among its elements, each refused
>         naming the element; `InertRP-1` refused naming `IC` and the missing monatomic
>         record;
>       - a fuel group of one mass-fraction and one mole reactant refused;
>       - `Fe2O3(cr)` at 1000 K accepted, its enthalpy equal to the joined table
>         species' at that temperature;
>       - `n-Butanol`'s enthalpy per kilogram equal to the liquid record's, compared with
>         the database record, not a typed value;
>       - a failed station with transport requested has a null `TransportStatus`, as
>         the corrected contract says.
>
>       The public surface does not move. No bit snapshot moves except the cases of a
>       fixture that names `n-Butanol` or a multi-record reactant, if any; the coder
>       checks for them and names them.
>
>       Ticked 2026-09-27, each fact shown red once against the pre-fix code and green
>       after, in the new `tests/Problems.Tests/AuditFixTests.cs`:
>       - `ARuleProblemValidationAppliesIsRefusedByTheRecordsOwnIndexNotABatchLocalOne`:
>         a batch of three records with the third's pressure 0 (a rocket problem,
>         non-shape rule) refused with a `StateRecordException` whose `Index` is 2,
>         red before the `noun`/`Refuse` change (a plain `ArgumentException` naming
>         `equilibrium problem 1` instead);
>       - `AnElementThatSurvivesOnlyInIonizedOrInertRecordsIsRefusedByName` and
>         `AnElementWithNoMonatomicRecordIsRefusedNamingWhatIsMissing` (naming `IC` of
>         `InertRP-1` and saying it has no monatomic record), both red before
>         `SpeciesSelection.ValidateElementsHaveCandidates` existed (the case solved,
>         or failed as `SingularMatrix`, instead of refusing);
>       - `MixedAmountKindsInOneRoleGroupAreRejected`: a fuel group of one
>         mass-fraction and one mole reactant refused, red before
>         `MixtureRule.ValidateOneAmountKindPerGroup` (the two amounts were summed
>         instead);
>       - `AMultiRecordProductNamesRangeIsTheUnionOfItsRecordsAndItsEnthalpyEqualsTheJoinedTable`:
>         `Fe2O3(cr)` accepted at 1000 K with its enthalpy equal to the joined table
>         species' at that temperature (an independent cross-check through
>         `SolverFixture.Shared.Engine` and `SpeciesFunctionBatch`, not a typed value),
>         red before `ReactantResolver.FromDatabase` took the union of every record's
>         interval (the first record's narrower range refused the case);
>       - `SeveralReactantOnlyRecordsOfOneNameResolveToTheLast`: `n-Butanol`'s
>         (`records[^1]`, the liquid) enthalpy per kilogram equal to the database
>         record's own field, not the gas record's, red before the `records[^1]` rule
>         (the first record, the gas, was taken).
>
>       `dotnet test tests/Problems.Tests`: 1117/1117, none skipped. The public surface
>       is unchanged (`tests/Protocol.Tests/PublicSurface.approved.txt` unchanged; the
>       `SurfaceTests` fact of the full run is green). No bit snapshot of this node
>       moves for these five fixes: `dotnet test APThermo.sln --filter
>       "Category!=LongRunning"` after every change here is green on the merged tree
>       (this criterion's own re-run count is in the merge's own record below), the
>       `BitSnapshotTests` of every node included, and the only lines any
>       `Bits*.approved.txt` of the tree moves on are the CSV-hash half of `Cli`'s
>       (its own criterion below); `n-Butanol` is the reactants-only section's only
>       multi-record name (`data/thermo.inp` lines 15478-15802 inspected by hand) and
>       is absent from every committed rocket and equilibrium fixture and approved
>       example, and no fixture names a product species as a reactant whose interval
>       union differs from its last record's own intervals. The `TransportStatus` doc
>       correction is a comment-only fix to `Results.cs` and this node's `API.md`; no
>       test changes because the code already matched the corrected wording
>       (`RocketTests.AFailingStationIsAStatusAndNotAnException` already pinned it).

---

<a id="audit-0926-transport-status"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-26, a failed station's transport status" — the contract's wording of the null transport status

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>     own status tells the two apart. The code did this already; the contract said
>     "null when transport was not requested", and the test that pinned the behaviour
>     asserted the opposite of the contract (`API.md` is corrected).

---

<a id="audit-0926-multi-record"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-26, a reactant name with several records" — the first record taken for both

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>     - Several reactant-only records of one name resolve to the last, as cea 3.3.4
>       does. The committed file has one such name, `n-Butanol`: gas then liquid. It
>       now resolves to the liquid, −3.757 MJ/kg as cea 3.3.4 gives, measured
>       2026-09-26 through the package's `calc_property`.
>     - ⚠ The first record was taken for both. That refused `Fe2O3(cr)` at 1000 K
>       (range "298.15-960 K"), which cea 3.3.4 evaluates (−4.533 MJ/kg). It also made
>       `n-Butanol` the gas, 369 kJ/kg above the reference.

---

<a id="audit-0926-amount-kinds"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-26, one amount kind per role group" — mass fractions and moles summed

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>     - ⚠ The two were summed as if a mass fraction were grams: 0.5 mass fraction of
>       H2(L) beside 0.5 mol of CH4(L) made the fuel 5.87 % H2(L).

---

<a id="audit-0926-no-candidates"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-26, an element with no candidate species" — the singular matrix of an element without species

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>     - ⚠ The element entered the table as a row without species, and the case came
>       back `SingularMatrix`, a numerical failure (exit 1), for an input the selection
>       rule does not support.

---

<a id="audit-0926-record-index"></a>

## 2026-10-01 — from "## Constraints", "audit fixes of 2026-09-26, a refused state record" — the plain ArgumentException of a non-shape rule

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>     - ⚠ The shape rules threw it; the others threw a plain `ArgumentException` worded
>       `equilibrium problem k` with the index of the problem inside the batch the runner
>       built. A caller that splits records into groups, as the command line does, then
>       named the wrong record.

---

<a id="criterion-packing-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria", "packing" — the throwaway consumer of the packed package

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

>       form carries no database path field to differ). A throwaway console project
>       referencing `APThermo` 0.1.0 from the feed, restored with ILGPU from nuget.org,
>       calls `SpeciesDatabase.LoadBundled()` and solves the LOX/LH2 rocket case of the
>       examples above through `Solver` on the CPU accelerator, printing the same chamber
>       temperature the library gives directly (3485.023295679567 K). Verified by hand
>       (packing is not part of `dotnet test`; the distribution phase's report has the
>       transcript), not by a committed test.

---

<a id="structure-packing-condensed"></a>

## 2026-10-01 — from "## Structure", "Packing" — condensed wording, with the read-only proof

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> **Packing (2026-09-15, distribution phase, root `BOOT.md`, `## Delivery`, Packages).**
> This node's project is the one packed as `APThermo`: `IsPackable=true`,
> `PackageId=APThermo`, the version and the shared package metadata (author, license
> expression, tags, symbols, SourceLink) from the root's `Directory.Build.targets`. The
> six nodes it depends on (`Data`, `Thermo`, `Equilibrium`, `Performance`, `Transport`,
> `Execution`) are never released apart from this one (root `BOOT.md`), so their
> assemblies are merged into this one package instead of becoming six more packages:
>
> - every `ProjectReference` to them carries `PrivateAssets="all"`, so none of them (or
>   anything they in turn reference) becomes a `<dependency>` of the `APThermo` nuspec;
> - a direct, unprivated `PackageReference` to `ILGPU` makes it the package's one real
>   dependency, since it would otherwise be suppressed along with the six nodes above
>   (`PrivateAssets="all"` on a `ProjectReference` suppresses everything that flows
>   through it, ILGPU included, unless the consuming project also references it
>   directly);
> - two MSBuild targets, hooked through `TargetsForTfmSpecificBuildOutput` and
>   `TargetsForTfmSpecificDebugSymbolsInPackage`, add the six referenced assemblies'
>   `.dll` to the package's `lib/net10.0` and their `.pdb` to the `.snupkg`, reading
>   `@(ReferenceCopyLocalPaths)` filtered to `ReferenceSourceTarget == 'ProjectReference'`
>   (this excludes ILGPU, a NuGet-sourced reference, from the merge). Both targets
>   declare `DependsOnTargets="ResolveReferences"` explicitly: `dotnet pack` invokes
>   each through its own narrow MSBuild sub-invocation of just that target, which
>   otherwise leaves `ReferenceCopyLocalPaths` empty. The `.pdb` list is derived from
>   the resolved `.dll` paths (`%(Filename).pdb` beside each, kept only if it exists)
>   rather than filtered from `ReferenceCopyLocalPaths` a second time: that item was
>   observed to carry the `.dll` entries but not their `.pdb` companions in this same
>   narrow sub-invocation, although a plain `dotnet build` of this project lists both;
>   the two entry points agree on every other fact used here (the extension, the
>   `ReferenceSourceTarget`, and the path itself once found).
> - the package's `README.md` (`docs/nuget/APThermo.md`) and the root's `data/NOTICE`
>   are packed as `None` items with `Pack="true"`; `NOTICE` comes from the shared
>   `Directory.Build.targets` (every packable project packs it the same way), the
>   README from this project alone (its `PackagePath` is the same `README.md` in every
>   package, but the source file differs per package).
>
> Proved once, read-only (a proof, not a permanent test: packing is not part of
> `dotnet test`): `dotnet pack src/Problems/APThermo.Problems.csproj -c Release`
> produced `APThermo.0.1.0.nupkg` with exactly the seven assemblies of this node's
> subtree in `lib/net10.0`, `README.md`, `NOTICE` and a nuspec naming only `ILGPU` as a
> dependency; `APThermo.0.1.0.snupkg` with the matching seven `.pdb` files; no warning
> on a machine with no git remote (`PublishRepositoryUrl=true` without a resolvable
> `RepositoryUrl` produced no diagnostic, checked because `TreatWarningsAsErrors=true`
> would have turned one into a build failure).

---

<a id="structure-element-order"></a>

## 2026-10-01 — from "## Structure", "The element order of first appearance" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> - **The element order of first appearance is stated once** (2026-09-15, the clean-code
>   repair's R-Problems-4). `Build`'s design named it one of the method's two duties
>   (F-PR-03: "validated the mixture rule and derived the element order"), but e15d02f
>   moved only the mixture rule out, to `MixtureRule.Validate`; the order itself stayed a
>   nested loop inline in `Build` (nesting 3), and the same rule (BOOT.md, Constraints)
>   was written a second time inside `ChemicalSystemCache.Union` (also nesting 3) for the
>   union of several mixtures' elements. `ElementOrder.OfFirstAppearance(IEnumerable<IEnumerable<string>>)`
>   states the rule once — every distinct symbol of a sequence of symbol lists, in the
>   order first seen — and both call it: `Build` over each resolved reactant's formula
>   symbols, `Union` over each mixture's own element list. Neither method's own loop
>   survives: `Build` now nests 1, `Union` 2 (its own validation loop, unrelated to the
>   element order, stays). Ce of `PropellantBuilder` and `ChemicalSystemCache` moves from
>   10 to 11, both still under the root's limit of 14, no `## Shape exceptions` row
>   needed. No behaviour change: `dotnet test tests/Problems.Tests`, 1111/1111;
>   `Bits.approved.txt` unmoved (26840f83).

---

<a id="structure-atomic-weights-two"></a>

## 2026-10-01 — from "## Structure", "the AtomicWeights row" — two translations of a missing atomic weight

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-15: `AtomicWeights`' row and its own summary read "the one translation of a
>   missing atomic weight into an `ArgumentException` naming the element". Wrong from the
>   type's introduction: `ReactantResolver.Custom` translates the same miss a second time,
>   naming the reactant as well as the element (present already in the
>   pre-decomposition `Reactants.cs` at `7661ea9`, carried through every decomposition
>   since). Unifying the two
>   into one call site would change a message, which is out of scope here; the row and
>   the type's summary now say what both translations do (the repair review's
>   R-Problems-8). Found by the clean-code repair review.

---

<a id="structure-runners-coupling-note"></a>

## 2026-10-01 — from "## Structure", "The runners are the pipelines' composition roots" — the result arrays SolveContext held

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-15: `SolveContext` is gone (the clean-code repair's R-Problems-1), so "the
>   result arrays `SolveContext` holds" no longer names anything: `SolveGroup` now
>   returns `RocketResult[]`/`EquilibriumResult[]` for its own group, and `Solve`
>   assembles the full `results` array itself from what each group returns; no field of
>   any type holds either array between calls any more. The over-count this sentence
>   explained has the same source as before and is independent of `SolveContext`'s
>   removal: `RocketResult`/`EquilibriumResult` (the object each loop inside
>   `SolveGroup` builds) and its array form (now the method's own return type, and the
>   type of `Solve`'s `results` local) name one type of the tree, not two, on the
>   protocol tests node's walk — which is why the measured coupling stayed 26 and 24
>   after the rewrite, unchanged from before it (`CouplingMeasures.EfferentCoupling`,
>   `RocketRunner`/`EquilibriumRunner`, above).

---

<a id="structure-solvegroup-shape"></a>

## 2026-10-01 — from "## Structure", "Size" — the two members over six parameters, HasFits and SolveContext

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-14, found at this close, by the root's IL walk over parameters
>   (`parameters.tsv`): two members over six parameters that were never declared an
>   exception and are not one — a plain oversight of the internal decomposition, not a
>   design decision. `RocketRunner.SolveGroup` and `EquilibriumRunner.SolveGroup` each
>   took nine (`system, table, cases, members, wantsTransport, kinds`/`targets, masses,
>   speciesNames, results`); what is fixed for the whole of one `Solve` call
>   (`system, table, cases, masses, speciesNames, results`) is now a private
>   `SolveContext` record struct built once, and each `SolveGroup` takes it plus the two
>   or three arguments that vary per group (`members, wantsTransport` and `kinds` or
>   `targets`) — four parameters. `ResolvedReactant` took eight; its enthalpy source
>   (`HasFits`, `AssignedEnthalpy`, always set together by `ReactantResolver`'s two
>   factory methods) is now init properties in the record's body, on `StateRecord`'s own
>   precedent, leaving six in the primary constructor. Both of `ReactantResolver`'s
>   construction sites updated to the object-initializer syntax the split needs; every
>   other read of either type's fields is unchanged (`resolved[k].HasFits`,
>   `r.AssignedEnthalpy` and the like read an init property exactly as they read a
>   constructor-set one). Neither is a coupling question, so neither waits on the design
>   session: `dotnet build` clean, the fast suite green unchanged (1111/1111 in this
>   node), `Bits.approved.txt` and `PublicSurface.approved.txt` unmoved.
>
>   ⚠ 2026-09-15: `HasFits` and `AssignedEnthalpy` are gone as stored properties (the
>   clean-code repair's R-Problems-2). Both were exactly what `ReactantResolver`'s two
>   factory methods could already derive from the constructor's own `Record` and
>   `Reactant.Definition` — `Record is { Intervals.Count: > 0 }`, and
>   `Record?.FormationEnthalpy ?? Reactant.Definition!.Enthalpy` — so storing them
>   duplicated state instead of reading it once (a record with a body, not a
>   parameter-count device: the primary constructor already counted six, without them,
>   as above). They are now computed properties; both construction sites drop their
>   object-initializer clause, and every read (`resolved[k].HasFits`,
>   `r.AssignedEnthalpy`) is unchanged, a computed property read exactly as an init one.
>   `dotnet test tests/Problems.Tests`: 1111/1111; `Bits.approved.txt` unmoved
>   (26840f83).
>
>   ⚠ 2026-09-15: `SolveContext` and its `SolveGroup` split are gone (the clean-code
>   repair's R-Problems-1). The "vary per group" description above was inaccurate before
>   the rewrite too, for `targets`: `Solve` built it once, the same length as `masses`
>   (`cases.Count`), and passed that one array unchanged to every `SolveGroup` call,
>   which read it by `targets[members[m]]` — exactly how `context.Masses[members[m]]`
>   read the field `SolveContext` did hold. Nothing about `targets` varied per group; it
>   belonged with `masses` among "what is fixed for the whole of one `Solve` call", not
>   beside `kinds`, which really was rebuilt per group (`RocketRunner` only: a fresh
>   `Enumerable.Repeat(...)` array inside `foreach (var key in order)`, one exit layout
>   at a time). The rewrite does not inherit the mistake by choosing a side of a
>   distinction it no longer draws: the case, the mass and, for equilibrium, the target
>   that admitting a case measured are one `AdmittedCase` record struct per case, built
>   once in `Solve`; each `SolveGroup` takes `system`, the group's own
>   `IReadOnlyList<AdmittedCase>`, `wantsTransport`, `speciesNames` and, for
>   `RocketRunner` only, `kinds` — and **returns** `RocketResult[]`/`EquilibriumResult[]`
>   for that group, instead of writing into a `results` array shared with every other
>   group the way `context.Results[members[m]] = ...` did; `Solve` places each group's
>   results back at their original indices. `RocketRunner.SolveGroup`: 5 parameters, 56
>   lines, nests 2. `EquilibriumRunner.SolveGroup`: 4 parameters, 45 lines, nests 1.
>   Efferent coupling unchanged at 26 and 24 (a nested record struct folds into its
>   runner, `AdmittedCase` exactly as `SolveContext` did). `dotnet test
>   tests/Problems.Tests`: 1111/1111; `Bits.approved.txt` unmoved (26840f83).

---

<a id="structure-solver-row-stale"></a>

## 2026-10-01 — from "## Structure" — the stale last sentence of the Solver row

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-15: the `Solver` row's last sentence stood "`RocketRunner` (Ce = 27) and
> `EquilibriumRunner` (Ce = 25) measure higher by the same walk, both over the textual
> limit of 10 and the walk-calibrated 14 the root records as of the integration branch's
> `9facd7f`; not this node's declaration to make, left to the design session after the
> merge. The two runners' Ce moved by one each, after this row was first measured, when
> their `SolveGroup` dropped from nine parameters to four (the parameter fix below): a
> `SolveContext` record struct now carries what `SolveGroup` used to take by six separate
> parameters, and it is one more type in each runner's own vocabulary". Stale since the
> design session actually held (the decision "The runners are the pipelines' composition
> roots" below, and the `## Shape exceptions` rows of 26 and 24): the sentence still read
> as if the runners' coupling were undecided and cited the pre-merge scratch figures
> (27/25) and the superseded textual limit (10), duplicating — and disagreeing with —
> what the rest of this document already states correctly. Found by the clean-code
> repair review (R-Problems-9); cut to a cross-reference instead of retold, per
> AGENTS.md §8 ("claims about a foreign node… go stale without the author's knowledge;
> link instead of retelling"), here applied to a claim about a different part of the
> same document.

---

<a id="structure-api-review"></a>

## 2026-10-01 — from "## Structure" — the API review of the distribution phase

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-15 (distribution phase): the API review of that day
> (its findings M1 and M2, fixed in `2bca252`) found two things this table did not
> record. `MixtureSpecification` (`MixtureRule`'s own file) had no consumer beyond this
> node's tests, since `Propellant.OxidizerToFuelRatio` already carries what a consumer
> needs without loss; it moved into `API.md`'s tree-contract section, and
> `Propellant.Mixture` became internal with it. `Station`, `RocketResult` and
> `EquilibriumResult` had public positional constructors no other assembly called; they
> are nominal now, with an internal constructor (`Station`'s properties `init`, for this
> node's own comparison-copy tests; the other two get-only). Neither change moves a row
> of the table below: `Propellant` stays `public, contract as API.md says` (it names no
> row of its own; see `API.md`) and `Station`/`RocketResult`/`EquilibriumResult` are
> result-record themes, not rows here either.

---

<a id="criterion-decomposition-2026-09-14"></a>

## 2026-10-01 — from "## Acceptance criteria", "the decomposition of `## Structure`" — condensed wording, with the shape evidence and the correction of 2026-09-15

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — The decomposition of `## Structure` (2026-09-14): every type within the
>       root's code-shape constraint (`Solver` and the two runners the declared composition
>       roots, their measured Ce written into the table); the tests node's front-door bit
>       snapshot unchanged, recorded before any code moved; every fixture theory green
>       unchanged; the surface moved only by the members `API.md` plans under 2026-09-14,
>       in one contract commit after the internal moves, with `PublicSurface.approved.txt`
>       moved in it; the command line's call sites adapted to the renames and to
>       `CustomReactantDefinition`, nothing else of it touched. Reformulated 2026-09-14: it
>       named `Solver` alone, the close measured the two runners over the limit, and the
>       design session declared them (the decision "The runners are the pipelines'
>       composition roots") rather than split them.
>
>       Shape, measured 2026-09-14 on the build of `a3b7d05`, merged as `765f4e2`: efferent
>       coupling by the dependency check's walk, `Solver` 22, `RocketRunner` 26 and
>       `EquilibriumRunner` 24 (the rows of `## Shape exceptions`), every other type of the
>       node 13 or below; no method or constructor declaring over six parameters outside
>       the three records' rows, whose single creations name their arguments; lines and
>       nesting by the close's reading until the protocol tests node's `ShapeTests` measures
>       them (no file over 270 lines, the longest method `RocketRunner.SolveGroup` at 53,
>       nesting at most 3, within the limit). The merge's fast suite green (3007 tests,
>       `Problems.Tests` 1111, `Cli.Tests` 85).
>
>       Bit snapshot: `git log --follow -- tests/Problems.Tests/Bits.approved.txt` names
>       one commit, `8f8263c` itself — no commit since has touched the file — and the
>       working tree carries no further diff against it either, checked repeatedly
>       through this close. Fixture theories: every L0–L2 theory of the tests
>       node green throughout the decomposition, 1111/1111 in `Problems.Tests` at the
>       close (no fixture skipped, none removed). Surface: `PublicSurface.approved.txt`
>       regenerated once, in the contract commit, its diff read in full against the
>       contract's `API.md` before approving (net +6: `CustomReactantDefinition` and
>       `StateRecordException` gained, `RocketSweep` dropped, `Reactant.Custom` and four
>       `Solver` members changed signature); unmoved since. Command line: `git diff
>       --stat 8f8263c..HEAD -- src/Cli` names only `src/Cli/Solving.cs`, the two
>       authorized mechanical fixes (`MixtureOf`/`CandidateSpeciesFor` renames,
>       `CustomReactantDefinition` construction), no rule of the command line changed.
>
>       ⚠ 2026-09-15: this criterion's Shape paragraph read "nesting at most 2". Wrong
>       already at the close: `ChemicalSystemCache.Union`, `Reactant.Reactant` and
>       `PropellantBuilder.Build` each nest 3 (an `if`/`for` chain), within the root's
>       limit of 3 but above what this paragraph claimed. Corrected to the true figure;
>       found by the clean-code repair review (R-Problems-10). The same review found
>       `RocketCase` and `EquilibriumCase` declared inside their runners' files, against
>       this section's own "one type per file"; moved to `RocketCase.cs` and
>       `EquilibriumCase.cs`, needing no further correction here.

---

<a id="structure-runners-condensed"></a>

## 2026-10-01 — from "## Structure", "The runners are the pipelines' composition roots" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - **The runners are the pipelines' composition roots** (added 2026-09-14 by the design
>   session, after the close measured them). `RocketRunner` and `EquilibriumRunner` name
>   both sides of the engine's boundary: the front door's cases, problems and records,
>   and the execution node's batch, result and transport types, with the thermo,
>   performance and transport structs a station carries; efferent coupling as the
>   protocol tests node defines it measures 26 and 24. They hold no formula: the numbers
>   they touch are copied into the batch or read back through `StationFactory`, the mass
>   through `MixtureMass`, the checks through `ProblemValidation`; what they decide is the
>   batching (by exit layout, by the transport flag) and the order of the engine runs.
>   The root's exception covers a composition root that holds no formula, as it covers
>   the execution node's four pipelines on the other side of the same boundary. A split
>   into a batch filler and a result assembler was weighed and not taken: the assembler
>   alone reads enough of the batch result to stay near the limit, for two more types
>   and no rule made clearer. The close's note that the two "hold real rules, the kind
>   the root's exception clause does not cover" read the clause as excluding any rule;
>   it excludes a formula. The scratch reproduction of the walk lists 27 and 25: it
>   counts `RocketResult[]` and `EquilibriumResult[]`, the result arrays `SolveContext`
>   holds, apart from `RocketResult` and `EquilibriumResult`, while an array of a type of
>   the tree adds no type of the tree.

---

<a id="structure-decisions-condensed"></a>

## 2026-10-01 — from "## Structure", "decisions taken with the review of 2026-09-14" — condensed wording of the state record, RocketSweep and Custom decisions

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - **The state record is this node's exchange shape** (F-AR-02, option a, decided at
>   the root). `StateRecord` keeps its five positional parameters and gains
>   `AreaRatios`, `PressureRatios` and a nullable `Flow` as init properties, so that no
>   construction site changes and its constructor stays within the parameter limit;
>   `HasExits` is the one statement of the kind rule; `SolveRocketStates` solves the
>   records with exits; `StateRecordException` carries `Index` and `Reason`, so that a
>   caller renames the subject without re-deciding a rule. The command line's copies of
>   the rules leave in its own design session.
> - **`RocketSweep` is retired** (F-PR-06). The command line expands its sweeps itself,
>   over a wider product (ratio, pressure and temperature, rocket and equilibrium), and
>   the library's batch is the list of mixtures with the list of problems. The sweep
>   overload built its system from the first case alone and bypassed the union path: a
>   second implementation of one rule, with its own behaviour and one consumer, its own
>   tests. The root `API.md`'s example follows in the same commit.
> - **`Reactant.Custom` takes a `CustomReactantDefinition`** (F-PR-05): formula,
>   enthalpy, temperature and the optional molar mass, the values that exist only
>   together on a custom reactant, travel as one record, which `Reactant.Definition`
>   exposes in place of the three nullable properties; the factory drops from eight
>   parameters to five, and the two adjacent doubles can no longer be swapped silently.

---

<a id="structure-decisions-intro"></a>

## 2026-10-01 — from "## Structure" — the introduction of the decisions

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> Decisions taken with the review of 2026-09-14. The contract-moving ones are coded in
> the contract commit, after the internal moves: `API.md` rewritten with these as real
> ✅ blocks and ⚠ corrections where the old declarations stood, and
> `PublicSurface.approved.txt` moved in the same commit.

---

<a id="structure-solver-row"></a>

## 2026-10-01 — from "## Structure", "the Solver row of the table" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> | `Solver` | the composition root: owns the engine and the collaborators below, turns each public entry point into (system, cases) and hands them to a runner; holds no rule. The declared exception to the coupling limit: it names the public problem and result types, the engine and its collaborators. Ce = 22 (`AcceleratorInfo`, `ChemicalSystem`, `ChemicalSystemCache`, `ElementalMixture`, `Engine`, `EngineOptions`, `EquilibriumCase`, `EquilibriumProblem`, `EquilibriumResult`, `EquilibriumRunner`, `MixtureMass`, `Propellant`, `PropellantMixtures`, `RocketCase`, `RocketProblem`, `RocketResult`, `RocketRunner`, `SpeciesDatabase`, `SpeciesSelection`, `StateBatchOptions`, `StateRecord`, `StateRecords`), measured 2026-09-14 after the contract commit (a manual signature-and-body count, `RocketSweep` dropping out with its removal), down from 23 after the internal decomposition and 34 before either; the dependency check's own IL walk (fields read and members called, not only signatures) agrees at 22, run on this node's build at `ef54a4a` with the root's scratch tool. The reason for the declared exception is what it names, not a superlative: the two runners are declared composition roots as well (the decision "The runners are the pipelines' composition roots") | public |

---

<a id="structure-intro-condensed"></a>

## 2026-10-01 — from "## Structure", "the opening paragraph" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). `Solver`
> was one class of 663 lines with an efferent coupling of 34, holding eleven
> responsibilities (the review's F-PR-01); `PropellantBuilder.Build` validated the
> mixture rule and derived the element order in 77 lines (F-PR-03). The data flow is
> `Solver` → `PropellantMixtures` (the propellant front door only) →
> `ChemicalSystemCache` → `ProblemValidation` and `MixtureMass` per case →
> `RocketRunner` or `EquilibriumRunner` → the engine → `StationFactory` → the result
> records. Every type below is internal except where marked; one type per file, named
> after the type; the public records keep their theme files.

---

<a id="constraints-unit-conversions"></a>

## 2026-10-01 — from "## Constraints", "units at this boundary" — the number of unit conversions

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-14: stood "the one conversion this node makes besides mass
>   normalization". The code held three, written as bare `1.0e3` and `1.0e-3` at five
>   sites (the clean-code review's F-PR-10): an absolute word without proof, the kind
>   `AGENTS.md` §8 names. Corrected to the list above, with the constants.

---

<a id="invariants-mass-tolerance"></a>

## 2026-10-01 — from "## Invariants", "Element moles describe one kilogram" — the derivation of the default tolerance, the first wording and the design session

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-13. What stays at the pointer is the current rule. The text as it stood:

>   `Solver.MassOf`, so that a raised tolerance never hides the figure. The default is
>   derived from what must pass and what must fail:
>   - must pass: a record built with the database's own atomic weights differs from
>     one kilogram by the rounding of its digits (the record of 2026-09-13 below:
>     1.5e-5); a record built from a reactant record's own molar mass carries that
>     record's rounding (the element moles of every fixture file, 195 on 2026-09-13,
>     are within 1.7e-5 of one kilogram, the largest 1.6502e-5 from the `Air` record's
>     28.9651159 kg/kmol against its formula's 28.96561, a bound the front door tests
>     node checks over the directory listing; the reactant records `HAN` and
>     `LMP-103S` are rounded by 4.5e-4 and 1.5e-4); a record built with another
>     atomic-weight table differs by that table's last digits (below 1e-4); a
>     simulation that omits its trace elements loses their mass, and an element worth
>     more than a percent of the mass is no trace;
>   - must fail: the nearest plausible mistakes are a composition per kilogram of one
>     reactant instead of the mixture (LOX/LH2 at an oxidizer-to-fuel ratio of 6: 17 %
>     off per kilogram of oxidizer, sixfold per kilogram of fuel), per pound (0.4536 kg,
>     54 % off), per two kilograms (100 %), in mol/g or kmol/kg (a thousandth), in
>     mmol/kg (a thousandfold), per 100 g (tenfold);
>   - so any tolerance between 1e-2 and 0.17 separates the two; it is set at the lower
>     end, 1e-2, so that the largest legitimate deviation passes and anything larger,
>     which is no longer a trace omission but a different mixture, fails. The nearest
>     plausible mistake is seventeen times the tolerance; the largest legitimate
>     deviation measured, the `HAN` record's rounding of 4.5e-4, is twenty-two times
>     below it.
>
>   ⚠ 2026-09-13: until this date nothing checked the mass. A record of another
>   simulation (C, H, O, N, Cl, Al; 1000.015 g) solved to 2701.37 K at 6.5 MPa, and
>   the same record with every element mole doubled solved without a message to
>   2799.63 K; a composition in mol/g or kmol/kg passed the same way, against the
>   root's intent and the command line's promise that a unit mistake cannot pass
>   silently. The propellant path is held to the same check: a reactant record whose
>   molar mass contradicts its formula yields a mixture of the wrong mass, and the
>   committed file has one (`ADN`, 630.0 kg/kmol against its formula's 124.06), so
>   such a propellant is refused instead of being solved for a fifth of a kilogram.
>
>   2026-09-13, design session, the decisions behind the declared tolerance. The check
>   knows `Σ n_i A_i`; only the caller knows why a record deviates: trace elements
>   omitted (a few percent, always a deficit), another atomic-weight table (about 1e-4,
>   but up to 0.85 % for lithium and 0.14 % for boron, whose natural abundances vary),
>   a reactant record's rounding (4.5e-4). A declared tolerance lets the caller state
>   that knowledge and keeps the check for everything beyond it. A "warning" mode
>   would not: it would solve the record as given, and such a solution is wrong in
>   every per-kilogram figure (a tp state keeps its mole fractions, but molar mass,
>   density, enthalpy, entropy and heat capacities carry the factor; an hp state's
>   temperature carries it too, 2701 K against 2800 K for the doubled record), and once
>   set in a script it lets the next thousandfold error through with exit code 0, which
>   is the silence the check exists to end. A normalization of the moles to one
>   kilogram is not offered either: it would guess the basis of the enthalpy, and the
>   reference's own `b_i` are not normalized (the fixtures deviate by up to 1.65e-5
>   because the reference divides by each reactant record's molar mass), so a default
>   normalization would move the tree off the reference. The user's records are per
>   kilogram; should records that are proportions ever appear, a declared basis is the
>   honest feature, not a warning. Coded the same day; the tests of the declared
>   tolerance use the record made heavy, because made light it does not converge: at
>   6.5 MPa its enthalpy sits 1.4 K above the 2700 K interval boundary of the `ALN(L)`
>   record, across which the tree's enthalpy of the mixture jumps by 357 kJ/kg (the
>   record's two fits differ by 68 kJ/mol there), and an assigned enthalpy inside that
>   gap has no solution on either side: the equilibrium node's open defect at a
>   transition with variable temperature, in a new place. Recorded here because it was
>   found here; the fix is that node's. 2026-09-13, the design sessions of the same
>   day assigned the fix: the Thermo builder cuts such a record at its jump
>   (join-and-cut) and the equilibrium node holds a pinned pair there, so an assigned
>   enthalpy inside the gap settles on the 2700 K plateau — coded the same day: the
>   record made light solves through the front door
>   (`SplitRecordTests.AnEnthalpyInsideTheALNGapSolvesThroughTheFrontDoor`)
>   and the gap is closed.

---

<a id="constraints-mole-fractions"></a>

## 2026-10-01 — from "## Constraints", "results" — mole fractions over all species

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-12: stood "mole fractions of the gaseous phase and mass fractions of
>   condensed species". The reference reports mole fractions over all species, and a
>   result that compares to it without a conversion is worth more than a gas-phase
>   convention nobody asked for; the condensed mass fractions stay.

---

<a id="dependencies-equilibrium-link"></a>

## 2026-10-01 — from "## Dependencies" — the missing link to Equilibrium

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-12: the root's decomposition listed this node's dependencies without
> `Equilibrium`. The kind of an equilibrium problem is `Equilibrium`'s `ProblemKind`,
> which the execution node's batch takes and this node's `EquilibriumProblem` exposes;
> a link is truer than a retold enum, and the root records the same.

---

<a id="invariants-temperature-margin"></a>

## 2026-10-01 — from "## Invariants", "Element moles and enthalpy" — the temperature margin

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-12: stood "a reactant temperature outside the record's range is an error,
>   not an extrapolation". The reference's own notion of a record's valid range is the
>   fit range, or the assigned temperature ± 10 K for a record without fits, and it
>   evaluates the polynomial without a range check: the AP/HTPB/Al fixtures carry
>   `AL(cr)` at 298.15 K against its 300 K lower bound, and a strict rule would reject
>   the reference's own inputs. The margin mirrors the reference's ± 10 K.

---

<a id="invariants-candidates-condensed"></a>

## 2026-10-01 — from "## Invariants", "Candidate species" — condensed wording, with the ion rule's correction

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

> - **Candidate species are chosen by one rule**: every gaseous product species of the
>   database whose elements are all among the mixture's elements, then every condensed
>   product species under the same condition, each in database order, minus the `Omit`
>   list, or exactly the `Only` list when given; ionized species (the electron
>   pseudo-element `E` in the formula) and inert pseudo-element records are never
>   candidates in version 1; a name with several records (a condensed species with one
>   record per temperature range) is one candidate. An omitted name that is no product
>   species is ignored, as the reference ignores it: the RP-1311 example 3 omit list
>   names reactant-only species and old spellings.
>
>   ⚠ 2026-09-12: stood "ionized species (names ending in `+` or `-`, and `e-`)". A
>   trailing sign is no criterion: the database truncates names such as `C3H4,cyclo-`,
>   and forty neutral species end in `-`. The end-to-end comparison reported them "not
>   in the table" while the reference's product lists carried them. An ion carries `E`
>   in its formula, and that is what the rule reads.

---
