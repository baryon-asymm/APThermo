# ACCEPTANCE.md — Docs.Tests

Current acceptance criteria, their dated evidence and preserved history.
The node frame remains in BOOT.md.

## Acceptance criteria

- [x] R19 local immutable-policy consumers match the actual runner configuration.
  (2026-11-15: `ColdCliDiagnosticTests.LimitsMatchTheActualRunnerPolicy`, isolated
  17-second/3-second policy variants on Windows and Linux make frozen duplicated
  metadata red once and corrected metadata green once, without a real CLI or wait.
  Actual 300-second/5-second source bytes are restored. Final Docs pure controls
  10/10 and full Fixtures 198/198 on each OS; Windows surface/declaration/coverage/
  dependency/diagnostic/process-category checks 15/15. Evidence:
  `SCRATCH/audit/secondary-policy/alternate-policy-proof-v2.json`,
  `linux-alternate-policy-proof.json`, `windows-docs-pure-final/`,
  `linux-docs-pure-final/`, `windows-fixtures-final/`, `linux-fixtures-final/` and
  `windows-metadata-final/`. The only surface additions are the two policy getters.)

- [ ] R19: cold-call limit metadata follows the actual immutable Harness policy;
  an isolated alternate-policy control rejects the frozen duplicated literals
  without a real CLI or timed wait. All nine original diagnostic controls and
  both original cold invocation populations/arguments remain valid.

- [ ] R15: every actual cold CLI invocation retains identity, input hash, limits,
  start/end/outcome context before a failure can escape. Controlled normal/timeout
  runners prove result/exception/raw-byte identity; omitting context is old-red.
  Both actual hosted OS paths execute every generated invocation in both field
  and exact modes with the unchanged five-minute process deadline. Actual logs/TRX
  establish the new execution; they do not infer which historical49 command hung.


- [x] 2026-11-14 — Owner-approved A26 schema-input bytes match CLI delivery and the canonical resource; `CommandLineExampleTests` 4/4. Evidence: `SCRATCH/audit/schema-approval-proof.json`, `schema-approval-docs/schema-approval-docs.trx`.

- [x] 2026-09-17 — L1–L6 are green over the committed guide, samples and documents:
      `dotnet test tests/Docs.Tests`, 18 of 18 passed.
- [x] 2026-09-17 — Every level has been seen red once against a deliberate drift and
      reverted, nothing of the mutations committed:
      Red-once records per level and for the vacuous-population guards (`SnippetTests`,
      `SampleOutputTests`, `CommandLineExampleTests`, `LinkTests`, `SchemaValidationTests`,
      `GuideShapeTests`) → HISTORY.md#red-once-first-pass
- [x] 2026-09-17 (guide rewrite) — L3's last silent skip is closed:
      `InvocationsOf` used to `continue` past any fenced block of more than one
      non-empty line without ever looking at it, so a multi-line block that opened
      with an `apthermo …` invocation (a command synopsis with its output pasted
      below it, for instance) passed unseen. It now fails, naming the page and line,
      when such a block's first line is an invocation; a placeholder synopsis
      belongs in prose or a link to `src/Cli/API.md` instead (root `BOOT.md`,
      Delivery: Documentation, item 3 of the guide-rewrite task). Shown red once, then
      reverted; 18 of 18 passed afterwards → HISTORY.md#guide-rewrite-red-once

      ⚠ 2026-09-17: was an equilibrium example recorded without `--accelerator cpu`, now
      with it → HISTORY.md#guide-rewrite-typo
- [x] 2026-09-17 (second audit, closed in `657410d`) — every escape the
      second audit found in this node's checks is closed, and every new or changed
      check was shown red once against a deliberate drift and reverted, nothing of the
      mutations committed: `dotnet test tests/Docs.Tests`, 24 of 24 passed (18 before,
      plus L1's two new facts, L3's `cli-document` fact, L4's two new facts, and L7).
      - B1 (blocker): a C# fence with its marker removed now fails L1 instead of going
        unchecked — the `QuickStart` marker deleted from `README.md`,
        `SnippetTests.EveryCsharpFenceIsPrecededByASnippetMarker` red
        ("README.md:34: this C# fence is not preceded by a '<!-- snippet: name -->'
        marker"), reverted. "Marked" is removed from this document (the table above)
        and from `API.md`'s first row.
      - D14: a marker followed by prose instead of a fence now fails with a message
        instead of throwing — prose inserted between `README.md`'s `QuickStart`
        marker and its fence,
        `SnippetTests.EveryMarkedCsharpBlockEqualsItsSampleRegion` red
        ("README.md:34: the snippet marker is not followed by a fenced code block",
        no `NullReferenceException`), reverted. `GuideDocuments.TryFencedBlockAt`
        carries the fix and is shared by L1 and L3's new `cli-document` fact, so
        neither can repeat D14's mistake.
      - m10: the region-name set is now tied to the samples node's scenario list — a
        `QuickStartOrphan` region added to `samples/Samples/QuickStart.cs` (outside
        the scope of this task, mutated and reverted only to prove the check),
        `SnippetTests.RegionNamesAreExactlyEachScenarioClassNameOrThatNamePlusUsings`
        red ("region(s) named by no scenario's class name (or that name plus Usings):
        QuickStartOrphan"), reverted.
      - M1/X2: a runnable verb with a misspelled `--accelerator` no longer reads as a
        synopsis — `--accelerator cpu` misspelled `--acelerator cpu` in `README.md`'s
        `rocket` example, `CommandLineExampleTests` red ("is not a runnable example:
        does not pin '--accelerator cpu'"), reverted.
      - M1/X3: a misspelled input path no longer reads as a synopsis —
        `rocket.json` misspelled `rocet.json` in the same line, the same test red
        ("names no document committed under samples/cli/"), reverted.
      - M1/X4: an invocation on any line but the first of a fence is no longer
        silently unread — a comment line added before `README.md`'s `apthermo rocket`
        fence, the same test red ("a fenced block carrying an 'apthermo …' invocation
        must hold no other non-empty line … 1 invocation line(s) among 2 non-empty
        line(s)"), reverted.
      - M1/X8, X9: an inline `apthermo rocket problem.json` span with no
        `--accelerator cpu` and no committed input (`docs/nuget/APThermo.Cli.md:31`),
        and its JSON example never checked against `samples/cli/`
        (`docs/nuget/APThermo.Cli.md:18-29`) — both were live escapes on the second
        audit's HEAD, not a synthetic mutation: `CommandLineExampleTests` red on the
        unmodified document ("is not a runnable example: does not pin '--accelerator
        cpu'"), then the `EveryMarkedCliDocumentEqualsItsSamplesCliFileAndValidatesAgainstItsSchema`
        fact itself red on an empty population before any marker existed ("no
        '<!-- cli-document: path -->' marker was found"). Fixed by quoting
        `samples/cli/problems/rocket.json` verbatim under a `cli-document` marker and
        making the invocation runnable (`docs/nuget/APThermo.Cli.md`); both facts
        green afterward.
      - M4/L7: the status table forgetting a `CaseStatus` name now fails — the
        `NoTransportData` row deleted from `docs/guide/troubleshooting.md`,
        `StatusTableTests.TheStatusTableListsExactlyTheNamesOfCaseStatus` red
        (listing seven names against `CaseStatus`'s eight), reverted. The three
        missing rows (`ThroatNotFound`, `AreaRatioInvalid`, `NoTransportData`) and the
        completed `InvalidInput` meaning are added to the table, sourced from the
        Errors sections of `Thermo`, `Performance`, `Equilibrium` and `Transport`'s
        `API.md`, not from memory.
      - M5: the `OWNER/REPO` placeholder now fails L4 — the line "See
        https://github.com/OWNER/REPO for the repository." appended to `README.md`,
        `LinkTests.NoDocumentCarriesTheOWNERREPOPlaceholder` red, reverted. The
        two real placeholder links (`docs/nuget/APThermo.md:76`,
        `docs/nuget/APThermo.Cli.md:53` as the second audit found them) are removed
        until the repository exists publicly; the root `ACCEPTANCE.md`'s "The packages"
        acceptance criterion now carries that condition ("until it exists they carry no
        guide link", `ce114ea`) and is where the restoration is tracked (ma8, this
        task: the report a coding task writes is not part of the tree and is not a
        place a document may point to, AGENTS.md §11).
      - m1: `README.md` or `llms.txt` missing now fails L4 instead of shrinking the
        checked set — `README.md` moved aside, `LinkTests.TheRootGuideEntryPointsExist`
        red ("README.md does not exist at the repository root"), restored.
      - m6, three escapes in one fact: a reference-style link
        (`[text][id]` / `[id]: target`) to a non-existent file —
        `LinkTests.EveryRelativeLinkResolvesToAFile` red ("the link
        'docs/roadmap-does-not-exist.md' does not resolve to an existing file");
        an HTML `<a href="…">` to the same non-existent file — the same test red
        with the same message; a `#no-such-heading` anchor on a real link
        (`docs/guide/cli.md#no-such-heading`) — the same test red ("the anchor
        '#no-such-heading' … names no heading; known anchors: command-line, purpose,
        when-to-use, steps, errors, see-also"). Each mutated alone in `README.md` and
        reverted.
- [x] 2026-09-17 (third docs audit, this task) — every escape that review found in this
      node is closed, and every new or changed check was shown red once against a
      deliberate drift and reverted, nothing of the mutations committed:
      `dotnet test tests/Docs.Tests`, 27 of 27 passed (24 before, plus L1's new fact,
      the new L1b `ScenarioTableTests`, and L3's new `EveryJsonFenceIsPrecededByACliDocumentMarker`).
      - ma2: a snippet region moved into a decoy method its own scenario's `Run` never
        calls — `QuickStart.cs`'s `QuickStart` region relocated into a new, uncalled
        `Decoy` method, `SnippetTests.EachScenarioBodyRegionLiesInsideItsClassRunMethod`
        red ("the 'QuickStart' snippet region (0-based lines 40-41) does not lie inside
        'QuickStart.Run' (0-based body lines 13-34)"), reverted.
      - N1: an info string with attributes after the language name —
        `` ```csharp title="Program.cs" ``` `` with no marker, added to a scratch guide
        page — `SnippetTests.EveryCsharpFenceIsPrecededByASnippetMarker` red
        (still recognised as a C# fence by its first word, still failing for want of a
        marker), reverted with the scratch page.
      - N1b: a fence opened but never closed before the end of the document — the same
        scratch page's fence closer deleted,
        `SnippetTests.EveryCsharpFenceIsPrecededByASnippetMarker` red ("a fenced
        block opened here has no closing fence before the end of the document"),
        reverted.
      - N4: an unrecognised shell prompt — `` `user@host:~$ apthermo devices` `` in the
        scratch page's fence, `CommandLineExampleTests` red ("this line names 'apthermo'
        but is not recognised as an invocation after stripping a known prompt"),
        reverted; a positive control, `` `PS C:\Users\ed> apthermo devices` ``, passed
        in the same run, proving the strip itself (not just the failure path) works.
      - N8: `apthermo devices --outptu x` (the review's own example) in the scratch
        page, `CommandLineExampleTests` red ("'apthermo devices …' names an option
        '--outptu' its declared synopsis does not have"), reverted.
      - MA1/X9b: a JSON fence with no `cli-document` marker — first proven on the
        scratch page, then reproduced exactly as the review found it: the marker at
        `docs/nuget/APThermo.Cli.md:19` removed,
        `CliDocumentTests.EveryJsonFenceIsPrecededByACliDocumentMarker`
        red naming that file and line, reverted.
      - D10: two invocations of `samples/cli/problems/rocket.json` with different
        options — a second `apthermo rocket … --accelerator cpu --threshold 1e-3` fence
        added to `docs/guide/cli.md` next to the existing one,
        `CommandLineExampleTests` red ("the approved file for 'rocket-threshold-1e-3' is
        missing"), proving the two no longer collide on `rocket.approved.json`,
        reverted.
      - ma1: the scenario table's class column diverging from the code — `QuickStart`
        misspelled `QuickStartWrong` in `samples/Samples/API.md`'s "## Scenarios" table,
        `ScenarioTableTests` red, reverted.
- [x] 2026-09-17 (fourth documentation review, this task) — every minor that review
      found is closed, and every changed or new check was shown red once against a
      deliberate drift and reverted, nothing of the mutations committed:
      `dotnet test tests/Docs.Tests`, 28 of 28 passed (27 before, plus the new L0
      `FenceTagTests`).
      - Minor 1: `docs/guide/getting-started.md`'s "every `apthermo` example is run"
        now names the two exceptions, `devices` and `--version`, next to it.
      - Minor 2, the inline prompt: a scratch page's inline span
        `` `user@host:~$ apthermo devices` `` (an unrecognised prompt),
        `CommandLineExampleTests` red ("this inline span names 'apthermo' but is not
        recognised as an invocation after stripping a known prompt … 'user@host:~$
        apthermo devices'"); the same page's `` `PS> apthermo rocket
        samples/cli/problems/rocket.json --accelerator cpu` `` (a recognised prompt)
        passed in the same run, proving the strip itself works, not only its failure
        path.
      - Minor 2, the fence-vs-prose bare verb: the same scratch page's
        `` ```console\n$ apthermo species\n``` `` (a bare verb alone in its own
        fence), `CommandLineExampleTests` red ("the approved file for 'species' is
        missing"), proving it is now checked as a full invocation rather than
        silently skipped as a mention. All reverted; the scratch page is not
        committed.
      - Minor 3: a scratch page's untagged fence ` ``` ` around a JSON object,
        `FenceTagTests.EveryFenceCarriesATagFromTheAllowList` red ("this
        fence carries no tag from the allow-list (csharp, json, console): the fence
        is untagged"); the same fence tagged ` ```python `, the same test red
        ("found 'python'"); reverted. A regression control on the same page — a
        ` ```console ` fence carrying `` `apthermo devices --outptu x` `` — passed
        `FenceTagTests` (a `console` tag is allow-listed) and then failed
        `CommandLineExampleTests` on the misspelled option (N8), proving a
        `console`-tagged fence still reaches L3 after L0 was added; reverted.
      - Minor 4: `tests/Docs.Tests/BOOT.md`'s own citation of
        `CommandLineExampleTests.EveryJsonFenceIsPrecededByACliDocumentMarker`
        is corrected to `CliDocumentTests.…`, the class the fact has lived in since
        the third audit's move; `API.md`'s L3 row now credits `CliDocumentTests`
        alongside `CommandLineExampleTests`.
      - Minor 5: `src/Execution/Kernels.cs:10`'s and
        `src/Execution/APThermo.Execution.csproj`'s citations of
        `SCRATCH/api-review-report.md` (a path outside the tree, meaningless to a
        reader of the shipped XML documentation or the packed source) are removed;
        `grep -rn "SCRATCH/" src` returns empty, and the build stays warning-free
        (`dotnet build APThermo.sln`, 0 warnings, CS1591 included).
      - Minor 6: `src/Cli/API.md`'s "## Command line" block is corrected — `devices`
        gains `[--format json]` (`CommandTable.cs` already accepted it;
        `CommandLineExampleTests.cs`'s superseded `Synopsis` dictionary and the block
        itself did not declare it) and `equilibrium`'s unparseable `[same options]`
        is spelled out like `rocket`'s —
        with a ⚠ on the corrected lines. `CommandLineExampleTests.cs`'s typed-in
        `Synopsis` dictionary is replaced by `CommandSynopses.FromCliApi()`, which
        parses this block directly (new file, `tests/Docs.Tests/CommandSynopses.cs`).
        Shown red once: a scratch page's `` ```console\n$ apthermo devices --format
        json\n``` `` passed against the corrected block; `--format json` then removed
        from the block's `devices` line, `CommandLineExampleTests` red ("'apthermo
        devices …' names an option '--format' its declared synopsis does not have"),
        the block restored, the same page green again, reverted.
      - Minor 7: `SchemaCommand`'s exit-2 refusal on a missing or unknown name was
        undocumented in `CommandTable.cs`'s own `--help` text ("no name lists the
        embedded names", contradicted by `SchemaCommand.cs:11-14` and
        `src/Cli/API.md`'s Errors table); corrected to "a missing or unknown name
        exits 2, naming the known schemas". `tests/Docs.Tests/approved/cli/help.approved.txt`
        re-approved to the new bytes; `dotnet test tests/Cli.Tests`, 116 of 116
        passed, confirming no other approval of the help text needed a change.
- [x] 2026-09-17 (repository-links task) — L4 gained the self-repository link check:
      the package READMEs of `docs/nuget/` now point their guide references at
      `https://github.com/baryon-asymm/APThermo/blob/main/…` (the root `ACCEPTANCE.md`'s
      "The packages" acceptance criterion, restored: "before the first release the
      package READMEs link the guide on the public repository", now that the
      repository exists), and `LinkTests.CheckDocument` was extended to resolve such
      a link against the repository root instead of skipping it as external, in a new
      fact, `EverySelfRepositoryLinkOfAPackageReadmeResolvesToAFileOrDirectory`.
      Shown red once: `docs/guide/getting-started.md` misspelled
      `docs/guide/gettign-started.md` in `docs/nuget/APThermo.md`'s link, the new fact
      red ("the link '…blob/main/docs/guide/gettign-started.md' does not resolve to an
      existing file or directory of the repository"), reverted; nothing of the
      mutation committed. `dotnet test tests/Docs.Tests`, 29 of 29 passed afterward (28
      before, plus this fact). The `OWNER/REPO` rejection (`NoDocumentCarriesTheOWNERREPOPlaceholder`)
      is unchanged and still runs over both package READMEs.
- [x] 2026-09-17 (actions-and-badges task) — L4's `InlineLink` regex is fixed to read an
      image-wrapped-in-a-link badge by its outer target instead of its inner image
      source (the ⚠ above). Red once through a deliberately broken badge target; 29 of 29
      passed afterward (`dotnet test tests/Docs.Tests`) → HISTORY.md#badge-red-once
- [x] 2026-10-05 — One approved document per example on every platform (owner decision O5, item 13
      of release 0.2.2). The tree's own correctly rounded `Exp`, `Log` and `Pow` make the Windows
      and the Linux (WSL2) documents equal byte for byte: the six `*.linux.approved.json` files
      are deleted, `rocket.approved.json` was regenerated once (the numbers moved with the own
      math), and `EveryCommandLineExamplesApprovedDocumentMatchesItsPlatformExactly` (name kept)
      and the process fact compare the one file on both platforms under `Category=BitSnapshot`.
      The declared deviation of the next criterion (the six Linux copies that were no real
      recording) is lifted by it, and so is its per-platform file picking. The field-by-field
      comparison stays for the hosted runners, whose proof the orchestrator owns.
      Evidence: `CommandLineExampleTests`, Windows and WSL2 Ubuntu 24.04, one commit.
      ⚠ 2026-10-05: was a Windows and a Linux approved file per example, now one file → tests/Harness/HISTORY.md#one-record-2026-10-05
- [x] 2026-09-29 — The command-line examples' approved documents follow the root's
      platform rule (the owner's decision, root `BOOT.md`, Delivery: Documentation).
      - `approved/cli/<key>.approved.json` holds the Windows record and
        `<key>.linux.approved.json` the Linux one, picked by the running platform as
        the harness picks bit snapshots (`SplitApprovedFileName`/`ApprovedPathOf`,
        `OperatingSystem.IsLinux()`); `--help`'s `.txt` record is not split, since its
        plain-text usage has not been shown to differ between the platforms.
      - The exact comparison (`EveryCommandLineExamplesApprovedDocumentMatchesItsPlatformExactly`)
        carries `Category=BitSnapshot`, so it runs on the reference machine (locally
        and on the self-hosted release runners) and not on the hosted CI runners.
        ⚠ 2026-10-05: was "not on the hosted CI runners" (`Category!=LongRunning&Category!=BitSnapshot`),
        now the hosted runs filter `Category!=LongRunning` only and it runs on every runner.
      - Every runner, the hosted ones included, compares the same delivered document
        with the platform's approved file field by field
        (`EveryCommandLineInvocationIsACheckedExampleOrADeclaredSynopsis`, via
        the Harness node's `JsonFieldComparison.Mismatch`): the same members in the same
        order, every string and boolean equal, every number within 1e-9 relative (1e-9
        is four orders above the measured platform difference, about 1e-13, and five
        below the fixtures' tolerance rows). The comparison is one named function, not
        a per-test tolerance.

        ⚠ 2026-09-30: was `JsonFieldComparison` a class of this node, now `tests/Harness`'s
        → HISTORY.md#field-comparison-moved
      - Red once each, on the reference machine (Windows), both reverted by
        `git checkout` afterward and confirmed clean (`git diff --stat`, no residue):
        `tp-states.approved.json`'s `inputs.temperature` moved from `3000.0` to
        `3000.00005` (1.667e-8 relative) turned
        `EveryCommandLineInvocationIsACheckedExampleOrADeclaredSynopsis` red, naming
        the path, the field (`$.cases[0].inputs.temperature`) and the relative
        difference; reverted, the same file's `composition.H` moved one ULP in its
        last decimal digit (`...528607` to `...528608`, small enough to stay inside
        the field tolerance) turned `EveryCommandLineExamplesApprovedDocumentMatchesItsPlatformExactly`
        red ("bytes differ"), the field fact staying green on that mutation. Both
        facts green again once reverted.
      - ⚠ 2026-10-05: was a declared deviation (six Linux copies that were no real recording), now lifted, one record → HISTORY.md#linux-copies-lifted

      Evidence: `dotnet test tests/Docs.Tests/APThermo.Docs.Tests.csproj --no-build`,
      30 of 30 passed (22 m 39 s) on the unmutated tree, Windows, this task's commit.
      The two red-once runs above, each a single filtered fact
      (`--filter "FullyQualifiedName~CommandLineExampleTests.<name>"`), each failing
      with the message quoted above, each reverted and rerun clean.

- [x] 2026-10-03 — L3 through the real process (the test pyramid, root `BOOT.md`, Test time
      budgets): every invocation of the guide runs as a fresh process and equals its
      approved file, field by field (`EveryCommandLineInvocationRunAsAProcessMatchesItsApprovedDocumentFieldByField`,
      every runner) and exactly (`EveryCommandLineInvocationRunAsAProcessMatchesItsApprovedDocumentExactly`,
      `Category=BitSnapshot`), both `Category=EndToEnd`, on the Windows approved files
      unmoved; the in-process facts of L3 run on `CliInvocation`'s warm solvers and their
      approved files are unmoved too. Red once, a process-only change: `Program.Main`
      writing one byte to standard output before running turned both process facts red
      (`'x' is an invalid start of a value`, and `bytes differ`) while the in-process exact
      fact stayed green; the runner's streams swapped turned both red; both reverted. The
      in-process and process runs share one comparison, passed the run as a delegate so that
      no in-process fact reaches a process start (`Protocol.Tests.EndToEndTests` walks the
      call graph). Evidence, Windows, `APTHERMO_NO_CUDA=1`: 30 facts of the fast set in 44 s and
      the two process facts in 44 s, inside whole-solution runs on a machine other agents used.


- [x] 2026-11-15 — R15 local diagnostic mechanism: ColdCliDiagnosticTests 9/9,
  using injected runners only. Original forwarding control was 2 red/1 positive;
  original exception/result/argv/raw-array/token/stack identity, before-call context,
  failing output sink, duplicate ordinals and both comparison modes are checked.
  Pure actual parser/classifier witnesses generate identical original/final lists:
  all eleven calls per mode, same order and duplicate arguments/source entries.
  Evidence: SCRATCH/audit/cli-doc-diagnostics/frozen-controls.json,
  missing-context-red-v2.trx, injected-controls-final-v2.trx, population-proof.json,
  verification-summary.json. No real cold CLI or numerical suite was run locally;
  R15 actual-hosted criterion above remains unchecked for the root's cloud proof.
  Additional same-day evidence: injected-controls-trx-final.trx retains the JSON
  records through real ITestOutputHelper/TRX; old-source-adapter-proof.json invokes
  the original private RunToExit0 with an injected timeout and proves missing Data context.
