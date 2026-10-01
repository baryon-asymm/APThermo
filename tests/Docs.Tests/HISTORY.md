# HISTORY.md — Docs.Tests

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="field-comparison-moved"></a>

## 2026-10-01 — from "## Acceptance criteria" — JsonFieldComparison moves to the harness

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>         ⚠ 2026-09-30: this bullet, and the API.md row and the L3 row above, called
>         `JsonFieldComparison` "the one named function of the node", a class of
>         `tests/Docs.Tests` (`JsonFieldComparison.cs`, `internal`). The workflow step that
>         runs the packed tool's example needs the same rule and cannot reach an internal
>         class of a test node, so the class moved to `tests/Harness` (public, behaviour
>         unchanged) and this node's copy is deleted; the field facts here call it there.
>         The red-once records below were made on the class before the move and were shown
>         again through the moved class (the 1e-8 mutation red, 1e-10 green) on the same day.

---

<a id="levels-rewritten"></a>

## 2026-10-01 — from "## Purpose" — the table and every level's test rewritten after the first audit

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-17: the table above, and every level's test, were rewritten after the audit
> of that day (closed in `3c0d271`) found the previous forms could pass
> vacuously (an early return on an empty set: D1), checked marked blocks only while two
> guide C# blocks went unmarked (D2, together with the root's W1), ran one of the
> guide's several `apthermo` invocations and searched `docs/guide` alone (D3), excluded
> all of `docs/protocol` instead of `docs/protocol/templates` (D5), walked two directories
> of `samples/cli` non-recursively (D7), and were never shown red (D4). Each level now
> fails loudly on an empty population, and each was shown red once against a deliberate
> drift and reverted — see `## Acceptance criteria` below for the dated list. L1's rule
> also changed with the root's restored marker syntax (`BOOT.md`, Delivery, the ⚠ of
> 2026-09-17): a region may be quoted on several pages, so this table no longer requires
> "exactly once".

---

<a id="second-audit-levels"></a>

## 2026-10-01 — from "## Purpose" — the second audit: L1, L3, L4 widened, L7 new

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-17 (second audit, closed in `657410d`): the paragraph above went
> on to say that L1 "does not require every scenario to be quoted either, since the guide
> pages were not rewritten in this task … `samples/Samples/API.md` records which
> scenarios are quoted today and which are proven by L2 alone". That was true of the
> commit the first audit reviewed; the guide rewrite that followed it (merged as
> `e229d2e`, before this second audit) quotes every one of the twelve scenarios at least
> once, as `samples/Samples/API.md` itself now records. The sentence was found stale by
> this task's coder while closing the second audit's B1 (below) and is removed rather
> than silently corrected in place, per AGENTS.md §8. L1 was widened the same day: a C#
> fence with no marker at all now fails
> (`EveryCsharpFenceIsPrecededByASnippetMarker`, B1), and the region names
> `GuideDocuments.SnippetRegions()` finds are now tied exactly to the samples node's
> scenario list (`RegionNamesAreExactlyEachScenarioClassNameOrThatNamePlusUsings`,
> m10), closing a region defined in a method no scenario calls (X7) reading as quotable.
> L3 was rewritten in full: it used to read only the first non-empty line of a fence and
> never an inline code span (X4, X8), and it downgraded a `rocket`/`equilibrium`/`states`
> invocation with a missing `--accelerator cpu` or a misspelled input path to a silently
> uninspected "declared synopsis" instead of failing (X2, X3); a JSON document shown next
> to prose was never checked at all (X9). L4 gained reference-style links, HTML `href`s,
> anchor-fragment resolution and the `OWNER/REPO` placeholder rejection (M5, m6), and an
> explicit assertion that `README.md` and `llms.txt` exist (m1, closing X6). L7 is new
> (M4). `## Acceptance criteria` below carries the dated, red-then-reverted evidence for
> every one of these.

---

<a id="third-audit-escapes"></a>

## 2026-10-01 — from "## Purpose" — the third audit: a further round of escapes closed

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-17 (third audit, this task): a further round of escapes was closed. L1 gained
> a fourth fact and an L1b row: a snippet region could sit in a method its own scenario's
> `Run` never calls and still pass every existing L1 fact, since none of them read *where*
> a region lives (ma2); Roslyn now proves the region's line span lies inside `Run`'s own
> block body. `samples/Samples/API.md`'s scenario table, the list `ci.yml`'s packaged-
> library step reads instead of a typed-in one, was never checked against the code, so a
> renamed or reordered scenario could turn that CI step into a zero-row, vacuously
> successful loop (ma1); `ScenarioTableTests` now proves the table's name and class
> columns match `Program.Scenarios`/`ClassNameOf` in order, and the workflow step itself
> now fails on a zero row count rather than silently doing nothing. L3 closed four more
> escapes: a fence's info string was compared whole rather than by first word, so
> `` ```csharp title="Program.cs" ``` `` (N1, unused today, guarded against tomorrow) or a
> `` ```json ``` `` fence carrying attributes would misclassify; a fence opened but never
> closed before the end of a document was silently dropped rather than failing (N1b); a
> `` `PS> apthermo …` `` or other prompt-prefixed line was never recognised as an
> invocation, so it went unchecked by every fact below without failing anything (N4); and
> a declared verb's own tokens were never checked against its synopsis, so `apthermo
> devices --outptu x` passed as a counted mention with the typo untouched (N8,
> `src/Cli/API.md`, "## Command line"). MA1 closed a fifth: a JSON fence with the
> `<!-- cli-document: path -->` marker removed went entirely unread, rather than failing
> as an unmarked fence the way an unmarked C# fence already did (mirroring B1 above);
> every JSON fence must now carry the marker. D10 closed a sixth: the approval key was the
> input file's name alone, so two invocations of one input with different options (a
> different `--threshold`, say) would silently share one approved file instead of each
> needing its own; the key now folds in every token beyond the mandatory input and
> `--accelerator cpu`. ma10 narrowed the "declared, uninspected" synopsis list from five
> verbs to two (`devices`, `--version`, the ones whose output the root `BOOT.md` now
> names as machine- or release-dependent) and made `species`, `schema` and `--help` full,
> always-checked invocations — before this task only their two fixed forms (`species
> --find H2O`, `schema input`) ran, and `--help` never did, though none of the three has
> output that depends on the machine or the release. This much new checking pushed
> `CommandLineExampleTests.cs` past the root `BOOT.md`'s 400-line code-shape limit; its
> two `cli-document`-marker facts (`EveryMarkedCliDocumentEqualsItsSamplesCliFileAndValidatesAgainstItsSchema`,
> `EveryJsonFenceIsPrecededByACliDocumentMarker`) and their shared helpers move
> to a new file, `CliDocumentTests.cs`, in the same class-per-concern shape L1's own
> `SnippetTests` and `ScenarioTableTests` already use; no test changed behaviour by the
> move. `## Acceptance criteria` below carries the dated, red-then-reverted evidence for
> every one of these.

---

<a id="fourth-review-minors"></a>

## 2026-10-01 — from "## Purpose" — the fourth documentation review: a round of minors closed

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-17 (fourth documentation review, this task): a further round of minors was
> closed. L3 gained a new fence-vs-prose distinction: a bare verb (`` `apthermo species` ``,
> nothing after it) reached the same "bare mention, skip" branch whether it stood alone in
> a fence or inside prose, so a fence that only ever showed the bare command — never a
> demonstration a reader could copy and run — passed silently (minor 2's second finding);
> `FenceInvocationsOf`/`InlineInvocationsOf` now carry `IsFence` on every invocation they
> find, and `Classify` skips a bare verb only when `IsFence` is false. The inline half of
> N4 was never built: `InlineSpan` matched only a span already starting with `apthermo `,
> so a prompt-prefixed span (`` `PS> apthermo rocket …` ``) never reached the regex at all
> and was silently invisible to every fact below, the same escape N4 closed for fences in
> the third audit (minor 2's first finding); every inline span is now read, its prompt
> stripped the same way a fence line's is, and a span that names `apthermo` but is not
> recognised as an invocation even after stripping fails naming the page and line, exactly
> as an unrecognised fence line does. A new L0 closes minor 3: nothing checked a fence's
> *tag* itself, so an untagged or misspelled-tag fence (a broken JSON example with no
> ` ```json ` word on it, say) reached no fact at all — L1 and the `cli-document` facts
> each look only for their own tag and skip anything else; `FenceTagTests` now requires
> every fence of the checked documents to carry a tag from the allow-list (`csharp`/`cs`/
> `c#`, `json`, `console`). Minor 6 replaced N8's typed-in synopsis dictionary with
> `CommandSynopses.FromCliApi`, which parses `src/Cli/API.md`'s own "## Command line"
> block (`tests/Docs.Tests/CommandSynopses.cs`, new file): the copy had drifted narrower
> than the parser (`devices` accepts `--format` — `CommandTable.cs` — but the copy, and
> `API.md`'s own synopsis line, did not declare it), and nothing had ever compared the two.
> `src/Cli/API.md` is corrected (its own ⚠, minor 6) and the `equilibrium` line's
> `[same options]` placeholder — unparseable by a machine — is spelled out identically to
> `rocket`'s, the same options `CommandTable.cs` gives it. `## Acceptance criteria` below
> carries the dated, red-then-reverted evidence for minors 2, 3 and 6; minors 1, 4, 5 and
> 7 were prose and citation corrections with no new check to prove red.

---

<a id="badge-links"></a>

## 2026-10-01 — from "## Purpose" — L4 gained badges: the link regex read the image, not the target

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-17 (actions-and-badges task): L4 gained badges. `README.md`'s new badge row
> (the root `BOOT.md` names none of this — badges are a repository convention, not a
> tree claim) puts an image inside a link, `[![alt](image-url)](target)`, and
> `LinkTests`'s `InlineLink` regex, `\[[^\]]*\]\(\s*([^)\s]+)`, stopped at the image's own
> `]`: for `[![CI](img-url)](workflow-url)` it captured only `img-url`, the outer
> `workflow-url` — the link a reader actually follows — was never read by
> `EveryRelativeLinkResolvesToAFile` at all. Proven live before the fix: the
> licence badge's link pointed at `blob/main/LICENSE-DOES-NOT-EXIST`, and the full L4
> suite still passed (5 of 5), the same silent-pass shape as every escape recorded above.
> `InlineLink` now reads `\[(?:!\[[^\]]*\]\([^)]*\)|[^\]])*\]\(\s*([^)\s]+)`: one level of
> nested `![...](...)` is treated as part of the outer link's text, so the badge's own
> target is what gets captured and checked, and its image source (ordinarily an external
> host such as shields.io) is left alone as any other external link is. Shown red once
> against the same deliberate `LICENSE-DOES-NOT-EXIST` mutation with the fixed regex in
> place — `EveryRelativeLinkResolvesToAFile` red ("the link
> 'https://github.com/baryon-asymm/APThermo/blob/main/LICENSE-DOES-NOT-EXIST' does not
> resolve to an existing file or directory of the repository") — then reverted; nothing
> of the mutation committed. No new fact was added and no population changed, so the test
> count is unchanged: `dotnet test tests/Docs.Tests`, 29 of 29 passed, before and after.

---

<a id="ci-claim-unmeasured"></a>

## 2026-10-01 — from "## Constraints" — the claim 'and on CI' that nothing measured (D12)

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-17 (D12, this task): this bullet used to add "and on CI", a claim nothing
>   in the tree measured — no GitHub remote exists yet, so `.github/workflows/ci.yml` has
>   never run (root `BOOT.md`, "The packages" acceptance criterion, still unticked for
>   the same reason). What is measured: `dotnet test tests/Docs.Tests`, the CPU
>   accelerator, Windows, 2026-09-17 (this task), 27 of 27 passed. What waits: the same
>   suite on Linux, and the workflow's own two-OS matrix, from the first CI run (root
>   `BOOT.md`, Delivery: Continuous integration); neither is recorded anywhere in the
>   tree yet.

---

<a id="red-once-first-pass"></a>

## 2026-10-01 — from "## Acceptance criteria" — the red-once records of the first criterion set

Moved because the red-once records of a ticked criterion are provenance (MIGRATION.md item 4); the criterion keeps its tick, date, test names and count. The text as it stood:

>       - L1: a byte changed in `docs/guide/rocket.md`'s `RocketSolve` block —
>         `SnippetTests.EveryMarkedCsharpBlockEqualsItsSampleRegion` red.
>       - L2: `approved/samples/QuickStart.approved.txt` edited — `SampleOutputTests`
>         red for `quick-start`.
>       - L3: a byte changed in `approved/cli/rocket.approved.json` —
>         `CommandLineExampleTests` red; separately, `docs/guide` and `README.md`
>         renamed away made the committed `rocket.approved.json` an orphan of no
>         runnable example — the same test red with a different message ("approved/cli
>         file(s) with no matching runnable example: rocket"), proving the orphan guard
>         (D1) non-degenerate.
>       - L4: a link target in `README.md` renamed to a non-existent file —
>         `LinkTests.EveryRelativeLinkResolvesToAFile` red; a relative link added
>         to `docs/nuget/APThermo.md` — `LinkTests.NoPackageReadmeCarriesARelativeLink`
>         red.
>       - L5: an unknown field added to `samples/cli/problems/rocket.json` —
>         `SchemaValidationTests` red.
>       - L6: `## Errors` renamed to `## Errors (MUTATED)` in `docs/guide/rocket.md` —
>         `GuideShapeTests` red.
>       - Vacuous-population guards: `docs/guide` and `README.md` renamed away —
>         `GuideShapeTests` ("no guide page was found"), `LinkTests` (a link to the now-
>         missing `docs/guide/rocket.md` failed to resolve) both red rather than
>         vacuously green.

---

<a id="guide-rewrite-red-once"></a>

## 2026-10-01 — from "## Acceptance criteria" — the guide-rewrite criterion: red-once record and count

Moved because the red-once record of a ticked criterion is provenance (MIGRATION.md item 4); the claim stays. The text as it stood:

>       Delivery: Documentation, item 3 of the guide-rewrite task). Shown red by
>       adding a second line to `docs/guide/cli.md`'s `apthermo devices` fence
>       (`CommandLineExampleTests` failing with
>       "docs/guide/cli.md:57: a fenced block with more than one line must not open
>       with an 'apthermo …' invocation … apthermo devices"), then reverted; nothing
>       of the mutation committed. `dotnet test tests/Docs.Tests`, 18 of 18 passed
>       afterwards, over two new runnable examples (`apthermo equilibrium
>       samples/cli/problems/equilibrium.json --accelerator cpu`, `apthermo states
>       samples/cli/states/tp-states.json --accelerator cpu`) and four new guide
>       pages (`getting-started.md`, `gpu.md`, `data.md`, `troubleshooting.md`).

---

<a id="guide-rewrite-typo"></a>

## 2026-10-01 — from "## Acceptance criteria" — the equilibrium example recorded without its accelerator

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>       ⚠ 2026-09-17 (second audit): the equilibrium example above was recorded without
>       `--accelerator cpu`, unlike the states example next to it and unlike the actual
>       page (`docs/guide/cli.md:37`, which always carried it). Found stale by the second
>       audit's m8 while closing this task; corrected in place, a
>       typo of form rather than a claim that changed meaning (AGENTS.md §8).

---

<a id="badge-red-once"></a>

## 2026-10-01 — from "## Acceptance criteria" — the badge criterion: red-once record and count

Moved because the red-once record of a ticked criterion is provenance (MIGRATION.md item 4); the claim stays. The text as it stood:

>       source (the ⚠ above). Checking `README.md`'s new badge row against the docs tests
>       as they stood found the escape live, not hypothetical: with the old regex, a
>       licence badge deliberately pointed at `blob/main/LICENSE-DOES-NOT-EXIST` still
>       left `EveryRelativeLinkResolvesToAFile` green (5 of 5); with the fix in
>       place, the same mutation turned it red ("the link
>       '…blob/main/LICENSE-DOES-NOT-EXIST' does not resolve to an existing file or
>       directory of the repository"); both runs reverted, nothing of the mutation
>       committed. `dotnet test tests/Docs.Tests`, 29 of 29 passed on the corrected
>       `README.md` afterward — the same count as before, since the fix widens an
>       existing fact rather than adding one.

---
