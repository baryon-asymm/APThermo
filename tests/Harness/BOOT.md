# BOOT.md — Harness

## Purpose

The scaffolding the test nodes share: one CPU accelerator with the committed database
and the tolerance table, bit-for-bit comparison, bit hashes and the approval of their
snapshot files, the grouping of fixture cases into families for batch tests, and
(2026-09-16) the JSON-document helpers `JsonSchema` and `RunPropertyCut`, moved here
from `Cli.Tests` where their second consumer, the docs tests node, belongs. It holds no
formula and names no type of the nodes its consumers test, so it can move no result.

It exists because the same scaffolding stood copied in four to six test nodes: the CPU
fixture four times, the batch-family grouping three times, the bit comparison five
times (the clean-code review of 2026-09-14, F-TK-08 and F-TF-13), and the approval of
the bit snapshots that the same pass added to six nodes. A copy is changed in some
places and not in others; a node is changed once. Decided at the root, since the node
is a neighbour of every test node that uses it (`AGENTS.md` §11).

## Invariants

- **Nothing above `Data` and `Fixtures`.** The node names types of `Data` (the
  database) and `Fixtures` (the repository paths, the cases, the tolerance table) and
  no type of `Thermo`, `Equilibrium`, `Performance`, `Transport`, `Execution`,
  `Problems` or `Cli`: a harness that knew a species table or a problem would sit above
  the nodes its consumers test, and the root's dependency arrows would stop describing
  the tree.
- **No formula and no tolerance.** A comparison within a tolerance belongs to the
  consumer that derives it or to the fixtures node's table; this node compares bits.
- **Bits are raw bits.** Two doubles are the same when `BitConverter.DoubleToInt64Bits`
  agrees, so that signed zeros and NaN payloads are told apart; a bit hash is the
  SHA-256 of the little-endian bytes of the values in the order they were added, a
  string entering as its UTF-8 bytes; nothing is rounded or formatted on the way. The
  node hashes exactly as the snapshots recorded before it existed were hashed: every
  approved file of the tree stays byte for byte, and an encoding one snapshot needs and
  another does not is a second method of `BitHash`, never a re-approval.
- **An approval file is a tripwire, not a contract.** A snapshot line is keyed by its
  first field; `Problem` reports a key missing from the approved file or a differing
  line as a problem naming the key and how to approve, and writes the actual lines next
  to the approved file as `*.actual.txt` (git-ignored), never over it, from the first
  such problem on. `StaleKeys` reports the approved keys no run produced, as bare keys:
  wording the problem and deciding whether to write anything of its own is the
  consumer's, as the acceptance criteria below record for each one.

  ⚠ 2026-09-15: this bullet read "a key missing from the approved file, a differing
  line and an approved key that no run produced are each a problem naming the key and
  how to approve; on a problem the actual lines are written". True of `Problem`, not of
  `StaleKeys`: that method only returns bare keys (`ApprovedSnapshot.cs`), and wording
  them into a problem and writing an actual file is left to the caller, which is why two
  consumers word it themselves and, until 2026-09-15, four never called it at all.
  Found by the repair review of 2026-09-15 reading the code against the claim.
- **`*.actual.txt` is CRLF; the repository is LF.** `Problem`'s write
  (`ApprovedSnapshot.cs`, the `File.WriteAllLines` call in the dirty-write branch)
  joins lines with `Environment.NewLine`, CRLF on the Windows platform this tree
  targets (`## Constraints` of the root), while `.gitattributes` keeps every
  committed text file LF. Whoever approves a change by copying the actual file over
  the approved one normalizes the line endings first; the Cli.Tests re-approval of
  2026-09-15 did so.

  ⚠ 2026-09-17: "the Windows platform this tree targets" was true when this bullet was
  written (2026-09-14), narrower than the root's platform constraint since the
  2026-09-15 distribution-phase decision added Linux x64. On Linux `Environment.NewLine`
  is `"\n"`, so an actual file this node writes there is LF like the repository, not
  CRLF; the normalize-before-approving step only bites on Windows. Found while adding
  the per-platform bit snapshots below.
- **A field dump is a caller's opt-in** (2026-09-18, the bits-diagnostics task).
  `BitHash` keeps, alongside the hash it computes, the text of every value added, in
  order: a double round-trip (`"R"`, invariant culture, so a dump reads the same on
  every platform), an int in invariant culture, a bool as `"true"`/`"false"`, a string
  as added — `Fields`, readable before or after `ToHex`, since it is a separate list
  the hash's own disposal does not touch. `ApprovedSnapshot.Problem` takes that list as
  an optional third argument; on a problem it writes each value to its own line of a
  file beside the actual file, named from the actual file's own name and the key
  (sanitized): `<actual-base>.<sanitized key>.fields.txt`. A caller with a composite
  line over more than one `BitHash` (the Cli tests node's JSON-plus-CSV row) has no
  single `Fields` to offer and passes none, keeping the two-argument `Problem` it
  already had. The mechanism lives once here, so every Bits-level consumer can adopt it
  by passing its hash's `Fields` at the call site it already has; as of this task only
  `Problems.Tests` does (its `BitSnapshotTests`, the node the release investigation
  named) — the other five Bits-level consumers (`Cli.Tests`, `Equilibrium.Tests`,
  `Performance.Tests`, `Thermo.Tests`, `Transport.Tests`) do not yet call the three-
  argument overload, a gap recorded here rather than left to be assumed closed
  (`AGENTS.md` §8), and open for whichever of those nodes wants it next. `BitHash.cs`,
  `ApprovedSnapshot.cs`.
- **One approved file per platform, picked in one place.** The root BOOT.md's platform
  constraint (2026-09-17) keeps a Windows and a Linux record for every bit snapshot,
  since the CPU accelerator's `System.Math` calls the platform's C runtime and the two
  do not round the last bit alike. `ApprovedSnapshot.ApprovedPathFor` is the one place
  that chooses between `<name>.approved.txt` and `<name>.linux.approved.txt`; every
  consumer's Bits level calls it instead of building the choice itself, so a future
  Bits level needs no platform logic of its own. A node whose Linux bits equal its
  Windows bits still keeps both files, byte for byte identical, so the rule has no
  exception (`Thermo.Tests`, whose table holds no accelerator solve).

  ⚠ 2026-09-19: the root BOOT.md's platform constraint (⚠ 2026-09-18, the declared
  deviation from "every test runs on both platforms") holds that the bits are a
  record of the reference machine, not of the platform alone, and are compared
  exactly only there: in local runs and on the release's self-hosted jobs
  (`.github/workflows/release.yml`'s `cuda-windows` and `cuda-linux`, filter
  `Category=Cuda|Category=BitSnapshot`), never on the hosted CI runners (`ci.yml`;
  `release.yml`'s `matrix` job; filter `Category!=LongRunning&Category!=BitSnapshot`).
  Each Bits-level consumer's own fact carries `[Trait("Category", "BitSnapshot")]`
  (`Cli.Tests`, `Equilibrium.Tests`, `Performance.Tests`, `Problems.Tests`,
  `Thermo.Tests`, `Transport.Tests`); this node adds no trait and no CI knowledge of
  its own — `ApprovedSnapshot` still only picks the file, never who runs the test
  that reads it.
- **One host, CPU only.** `CpuHost` creates one ILGPU context and one CPU accelerator
  and loads the database (with `trans.inp`) and the tolerance table once; it never
  creates a CUDA accelerator.

## Dependencies

- [Data](../../src/Data/API.md) — the database the host loads.
- [Fixtures](../Fixtures/API.md) — the repository paths, the cases, the tolerance table.

Outside the tree: ILGPU 1.5.3 (the CPU accelerator only); the .NET base class library
(`System.Text.Json` for the JSON-document helpers); xunit 2.9.3, for `TheoryData<>` alone
(`FixtureFamilies.Keys`/`CasesOf`, 2026-09-24, so its callers' theory sources are typed and the
Diagnostics constraint's xUnit1042 has no untyped `object[]` row to flag) — this node stays a
plain library (`IsTestProject` false), not a test project: no `Microsoft.NET.Test.Sdk` or test
runner is pulled in.

⚠ 2026-09-25: the same-day entry above named a single method, `FixtureFamilies.Of`, returning
`TheoryData<string, int, IReadOnlyList<CeaCase>>`. Its callers (the kernel-equality tests of
`Equilibrium.Tests`, `Performance.Tests`, `Transport.Tests`) then failed xUnit1045: a `CeaCase`
collection is not a type xUnit knows how to serialize for Test Explorer's row enumeration, only
the fields of `TheoryData<>` themselves. `Of` is now `Keys` (theory data: key and count only,
both serializable) plus `CasesOf` (the family's cases, read back inside the test body, the same
pattern `HostSolver.CaseNames`/`Load` already uses for a single case). Found fixing the
Diagnostics constraint in those three test nodes.

## Constraints

- A library assembly (`APThermo.Harness`) referenced by test
  projects only; it references no test framework, so its public surface enters the
  protocol tests node's snapshot like any library's.
- Every type is a stable type in the root's sense once the test nodes use it: small,
  and named in `API.md`.
- Paths from the repository root through `RepositoryPaths` of the fixtures node.
- The node has no tests node of its own: it is proven through its consumers (the
  acceptance criteria below).

## Acceptance criteria

- [x] 2026-09-14 — The node holds the types of `API.md` and the copies are gone: the
      CPU fixtures of `Thermo.Tests`, `Equilibrium.Tests`, `Performance.Tests` and
      `Transport.Tests` (each keeps its collection definition, since xunit's
      collections are per assembly, and `Thermo.Tests` its upload helper); the
      batch-family grouping of the kernel-equality tests of `Equilibrium.Tests`,
      `Performance.Tests` and `Transport.Tests` (the last kept to its members with
      transport after the shared grouping, since a table key is independent of the
      transport flag); every `SameBits` and `BitDifferences` of `Equilibrium.Tests`,
      `Performance.Tests`, `Transport.Tests`, `Execution.Tests` (`BitEquality.cs`
      deleted) and `Problems.Tests` (`StationEquality`'s composite `Station`
      comparison stays, its own concern, but reads this node for the bit-exact
      leaves); the hashing and approval code of the Bits levels of `Thermo.Tests`,
      `Equilibrium.Tests`, `Performance.Tests`, `Transport.Tests`, `Problems.Tests`
      and `Cli.Tests` (the last two needed `BitHash.Add(bool)`, a one-byte encoding
      neither the four accelerator-comparison nodes' snapshots nor the fixtures node's
      own hasher had needed, added the same commit as `Problems.Tests`, the surface
      snapshot re-approved for it). Every one of the seven consumer nodes'
      `## Dependencies` links this node and the dependency check agrees; the surface
      snapshot gained this node's section in the commit that created it.
- [x] 2026-09-15 — Nothing moved: the whole tree's fast suite (10 projects, 3014
      tests) green, every `Bits.approved.txt` byte for byte unchanged (hashes below),
      every kernel-equality and batch bit-equality test green. The node seen red
      through its consumers three times, each mutation alone, restored immediately
      after and the fast suite confirmed green again:

      `Bits.Same` made to answer `false` (`Bits.cs`): 88 bit-equality tests red across
      six of the ten projects - Thermo.Tests 41 of 378, Transport.Tests 8 of 157,
      Equilibrium.Tests 9 of 463, Performance.Tests 8 of 699, Problems.Tests 9 of
      1111, Execution.Tests 13 of 41 - every one a test that calls `Bits.Same` or
      `Bits.Differences` directly. Data.Tests, Fixtures.Tests, Cli.Tests and
      Protocol.Tests stayed green: the first two do not depend on this node, and
      Cli.Tests calls neither method (its Bits level hashes rendered text; below).

      A double added to `BitHash.Add(double)` in big-endian order (`BitHash.cs`): 314
      Bits-level tests red across five of the six nodes with a `Bits.approved.txt` -
      Thermo.Tests 213 of 378, Performance.Tests 98 of 699 (both a theory per fixture
      case), Transport.Tests 1 of 157, Equilibrium.Tests 1 of 463, Problems.Tests 1 of
      1111 (each one fact over every fixture, so one assertion carries every moved
      hash). `Cli.Tests` (91 tests) stayed green: its Bits level hashes the CLI's
      rendered JSON and CSV text (`BitExamples.Sha256`, `BitHash.Add(string)`
      only, never a raw double), so this mutation does not reach it.

      A line for a fixture that does not exist added to
      `Transport.Tests/Bits.approved.txt` (a fabricated key and hash):
      `EveryFixtureWithTransportGivesTheRecordedBits` red, one problem naming
      the key - "tests/Fixtures/cases/rocket/does-not-exist_pc1MPa_shiftingEquilibrium.json:
      recorded in the approved snapshot, but no such fixture is run with transport"
      (`ApprovedSnapshot.StaleKeys`; wired by `Transport.Tests`, `Equilibrium.Tests`
      and, since 2026-09-15, `Cli.Tests` (this repair's R-Cli.Tests-1:
      `Every_example_gives_the_recorded_output` now calls `StaleKeys` too, worded the
      way `Problem`'s own messages are). `Thermo.Tests`, `Performance.Tests` and
      `Problems.Tests` still key their theories from the fixture directory alone and
      do not check for an orphaned approved line - a gap recorded here rather than
      closed silently, since fixing it edits three foreign nodes' own test code,
      outside 2026-09-15).

      ⚠ 2026-09-15: the gap named above in `Thermo.Tests`, `Performance.Tests` and
      `Problems.Tests` closed the same day, each in its own repair review
      (R-Thermo.Tests-1, R-Performance.Tests-1, R-Problems.Tests-1), citing this
      paragraph as the record of the gap. All six test nodes with a
      `Bits.approved.txt` (`Cli.Tests`, `Equilibrium.Tests`, `Performance.Tests`,
      `Problems.Tests`, `Thermo.Tests`, `Transport.Tests`) now call
      `ApprovedSnapshot.StaleKeys` (`git grep -n "StaleKeys" -- tests`, verified at
      the end sweep of 2026-09-15). No gap remains.

      `Bits.approved.txt` hashes, all six unmoved through the whole exercise:
      Cli.Tests `483c979b75b5c98b5e11ddc4359f28812e225e15`, Equilibrium.Tests
      `65788e23f4390305763c80ab1f66b2054ff1907a`, Performance.Tests
      `5aa32f2bbf679cdd0f47749b0780059ba89faa62`, Problems.Tests
      `26840f83c9be5a405b22afa49cd519a2b2413e37`, Thermo.Tests
      `8bd5068ebcd28a090a9ab1bd6b048f4c42da5d6b`, Transport.Tests
      `3e4000dbb3fc1340c4f5f67a77a7fac566482ae8`. The whole tree's fast suite
      confirmed 3014 of 3014 green again after every revert; `protocol_lint` 0
      errors, 0 warnings throughout.

      ⚠ 2026-09-15: the criterion as first written predicted "every Bits level red"
      for the big-endian mutation. Wrong for `Cli.Tests`: its Bits level hashes the
      adapter's rendered text output, not a raw double, by design (hashing the
      formatted text is the stricter check for a text-rendering node, since it also
      catches a formatting change no bit difference would) - a real, verified
      exception, not an oversight, recorded rather than forced to fit the absolute
      word (`AGENTS.md` §8).

      ⚠ 2026-09-15 (later the same day): "catches a formatting change no bit
      difference would" held for the CSV half only. Until this second correction
      `Cli.Tests`' JSON half hashed a compact re-serialization of the document
      (`JsonNode.Parse(json).AsObject()` with `run` removed, then `ToJsonString()`),
      which itself let the document's indentation, line breaks, the final newline and
      string escaping change unnoticed - exactly the kind of formatting change this
      sentence claimed hashing text caught. Found by the repair review of 2026-09-15
      (R-Cli.Tests-2). The JSON half now hashes the bytes the command line delivers for
      the document, with the top-level `run` property cut out by span rather than by
      re-serializing (`Cli.Tests` BOOT.md, the Bits level and the criterion of
      2026-09-15), so the sentence now holds for both halves of every line.
- [x] 2026-09-17 — `JsonSchema` and `RunPropertyCut` (moved here 2026-09-16 from
      `Cli.Tests`, `API.md`'s JSON documents section) are proven through their
      consumers, and nothing of either type moved in the move: `Cli.Tests`'
      `RunPropertyCutTests` (the cut exact wherever `run` sits among its siblings, a
      missing top-level `run` and a duplicated one each failing instead of hashing) and
      the schema validations of `Cli.Tests.InputDocumentTests` and
      `Cli.Tests.OutputDocumentTests` (every example document against the input, states
      and output schemas) and `Cli.Tests.SchemaCommandTests` (the schemas read directly
      through `SchemaResources`, not through `JsonSchema`, but exercising the same
      schema files `JsonSchema` validates elsewhere); `Docs.Tests.SchemaValidationTests`
      (every sample document against the input and states schemas, read through
      `Program.Run(["schema", name], …)` and `JsonSchema.Parse`) and
      `Docs.Tests.CommandLineExampleTests`. The test method names of `Cli.Tests` are
      unchanged at HEAD and in the working tree, and `Bits.approved.txt`'s hash is
      unchanged (`Cli.Tests` BOOT.md, the Bits level), so no test moved with the code.
      Found missing by the CLI audit's finding H1, fixed in `68f540a`.
- [x] 2026-09-17 — `ApprovedSnapshot.ApprovedPathFor` is the one place that picks a
      node's approved file for the running platform (`API.md`); `Cli.Tests`,
      `Equilibrium.Tests`, `Performance.Tests`, `Problems.Tests`, `Thermo.Tests` and
      `Transport.Tests` call it and name no platform in their own code
      (`git grep -n "ApprovedSnapshot.ApprovedPathFor" -- tests`). Shown red once by
      mutating it to always return the Linux name, on Windows: `dotnet test
      APThermo.sln --filter "Category!=LongRunning"` turned 68 Bits-level facts and
      theories red — `Transport.Tests` 1, `Equilibrium.Tests` 1, `Performance.Tests`
      64, `Problems.Tests` 1, `Cli.Tests` 1 — every one reading the Linux bits with the
      Windows accelerator; `Thermo.Tests` (379/379) stayed green, since its
      `Bits.linux.approved.txt` is the byte-for-byte copy the acceptance criterion
      below records. Reverted immediately after; the fast suite confirmed 3098/3098
      green again on Windows, `protocol_lint` 0 errors, 0 warnings.
- [x] 2026-09-17 — The first Linux run with this harness (WSL2 Ubuntu 24.04, .NET SDK
      10.0.112, this task's commit on top of `f67b1a9`) approved a
      `Bits.linux.approved.txt` for every node whose Linux bits differ from its Windows
      ones — `Cli.Tests`, `Equilibrium.Tests`, `Performance.Tests`, `Problems.Tests`,
      `Transport.Tests` — and, for `Thermo.Tests`, whose table builder calls no
      accelerator and no `System.Math` function, a byte-for-byte copy of
      `Bits.approved.txt` (hash `8bd5068ebcd28a090a9ab1bd6b048f4c42da5d6b`, identical to
      the Windows file's own, both recorded above), so the platform rule has no
      exception. `Bits.linux.approved.txt` hashes: `Cli.Tests`
      `dd080fd2a77c54047eb59260105368c4e1e80451`, `Equilibrium.Tests`
      `7a4c552e2219a2e81f6a866e9fcf6587001fb9f5`, `Performance.Tests`
      `af784d63cbca303f94464077c7015d509f93ec1c`, `Problems.Tests`
      `e6e4eaee20ba7a639f4e56903d59e253439a172e`, `Transport.Tests`
      `27fbcc06dc8af69b2cd7e386dca59ba54a3c2f87`. Before approving, the rest of that
      Linux run was confirmed green on its own: `APTHERMO_NO_CUDA=1 dotnet test
      APThermo.sln --filter "Category!=LongRunning"` gave 3098/3098 once the six files
      above were in place (every Bits-level failure before that was the missing-file
      case, `key: not in <path>`, on every fixture the run enumerated, never a
      mismatch), and `protocol_lint` gave 0 errors, 0 warnings.
- [x] 2026-09-18 — The per-case field dump (the bits-diagnostics task, "a field dump is
      a caller's opt-in" above) writes and reads back correctly, shown red once through
      its one adopting consumer, `Problems.Tests`: the last hex digit of
      `tests/Fixtures/cases/rocket/lox-lh2_of4_pc5MPa_frozenAtThroat.json`'s line in that
      node's `Bits.approved.txt` changed from `3` to `0`,
      `Every_fixture_gives_the_recorded_bits` red on exactly that key,
      `Bits.actual.tests_Fixtures_cases_rocket_lox-lh2_of4_pc5MPa_frozenAtThroat.json.fields.txt`
      written beside `Bits.actual.txt` with 173 lines, one per field `HashOf` adds, each
      a round-trip double, an invariant-culture int or a `true`/`false`; every other
      fixture's key stayed green and wrote no dump of its own. Reverted; the fast suite
      confirmed green again (`Problems.Tests` 1111/1111) and `Bits.approved.txt`
      byte for byte unmoved (`git diff` empty). The dump copied out before the revert is
      `SCRATCH/bits-diag/reference-lox-lh2_of4_pc5MPa_frozenAtThroat.txt` (that task's
      report), a reference a hosted-runner run's own dump of the same fixture can be
      diffed against.
- [x] 2026-09-19 — Release configuration reproduces the approved bits on both
      platforms, closing the open question the 2026-09-17 Linux approval left: that
      approval ran `dotnet test APThermo.sln --filter "Category!=LongRunning"` with no
      `-c` flag, i.e. Debug, while `release.yml`'s self-hosted jobs run
      `--configuration Release`. On the reference machine, `dotnet test APThermo.sln -c
      Release --filter "Category=Cuda|Category=BitSnapshot"` (the exact command
      `cuda-windows` and `cuda-linux` run): on Windows, every BitSnapshot-category fact
      of the six nodes with a `Bits.approved.txt` and every Cuda-category correctness
      fact (`Execution.Tests`' 100 000-case sweep included) passed against the
      Windows-flavoured approved files, unmoved; in a fresh clone under WSL2 Ubuntu
      24.04 as user `student` (`/home/student/aptherm-rehearsal`, cloned from
      `worktree-agent-a0efb38cbae4b4f4f` at `b17d79f`), the same command passed in full
      (330/330) against the Linux-flavoured approved files, unmoved. Release
      reproduces the bits this node's `ApprovedPathFor` picks on both platforms; no
      `*.approved.txt` moved on either run (`git diff` empty in both trees).

      One fact of `Execution.Tests` is outside this criterion's claim and this node's
      concern (its Invariants: "no formula and no tolerance"):
      `CudaTests.Throughput_is_recorded_and_not_below_the_approved_ratio` is a
      performance tripwire, not a bit comparison, and on this run of the Windows
      reference machine it measured 22–29× against the required ≥45.02× (80 % of the
      approved 56.28×) on five separate invocations (solution-wide and isolated, with
      and without MSBuild's `-m:1`), while the same command was green on the Linux
      clone. `nvidia-smi` showed the GPU at P8 (487 MHz of a 3090 MHz boost clock) at
      rest between runs. Every other fact of `Execution.Tests` under this filter (bit
      correctness, the sweep) stayed green throughout. Reported to the node that owns
      the tripwire and its approved file (`tests/Execution.Tests`), out of scope here:
      this task touched no `src/`, no test code outside this node, and no approved
      file.

## Taboos

- No formula, no tolerance, no type of a numerical node, of the front door or of the
  adapter.
- No test of a node's behaviour here: this node is scaffolding.
- No CUDA accelerator.
