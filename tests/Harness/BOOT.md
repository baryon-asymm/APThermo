# BOOT.md — Harness

## Purpose

The scaffolding the test nodes share: one CPU accelerator with the committed database
and the tolerance table, bit-for-bit comparison, bit hashes and the approval of their
snapshot files, the grouping of fixture cases into families for batch tests, and
(2026-09-16) the JSON-document helpers `JsonSchema` and `RunPropertyCut`, moved here
from `Cli.Tests` where their second consumer, the docs tests node, belongs, and
(2026-09-30) `JsonFieldComparison`, the field-by-field comparison of two documents, and
(2026-10-03) `DotnetProcess`, a built assembly run as a separate process, moved here from
`Cli.Tests` for its second consumer, `Docs.Tests`. It holds no
formula and names no type of the nodes its consumers test, so it can move no result.

It exists because the same scaffolding stood copied in four to six test nodes: the CPU
fixture four times, the batch-family grouping three times, the bit comparison five
times (the clean-code review of 2026-09-14, F-TK-08 and F-TF-13), and the approval of
the bit snapshots that the same pass added to six nodes. A copy is changed in some
places and not in others; a node is changed once. Decided at the root, since the node
is a neighbour of every test node that uses it (`AGENTS.md` §11).

## Invariants

- JSON result comparisons accept only values converting to finite double output.
  Invalid/unrepresentable/nonfinite numeric output is a path-named mismatch, before
  relative-tolerance arithmetic. The existing relative budget is unchanged (IT01).

⚠ 2026-11-14: IT01 independently reproduced 1 versus 1e400 as an accepted result:
Infinity <= Infinity arose after conversion. Whole-entry finite-domain controls
replace that false equality; schema numeric semantics are a separate obligation.

- Process waits and stream drainage share a deadline. The existing four-argument
  `DotnetProcess.Run` delegates to a five-minute default; an additive six-argument
  overload accepts timeout/cancellation. Timeout must be positive and representable
  by the .NET timer (at most uint.MaxValue-1 milliseconds). Cancellation already
  requested prevents launch. Expiry throws TimeoutException; caller cancellation
  throws OperationCanceledException carrying that caller's token.
  On interruption attempt whole-process-tree kill and bounded reap/drain, with a
  separate cleanup window of at most five seconds; inherited pipe handles cannot
  make the caller wait indefinitely. Exceptions preserve captured partial raw bytes
  in Data["StandardOutput"] and Data["StandardError"] (byte[]), plus bounded readable
  stdout/stderr excerpts. Capture and snapshot operations are synchronized.

⚠ 2026-11-14: the former process runner waited indefinitely for process/stream exit.
T14 adds the deadline and cancellation contract while retaining raw-byte/argument/
environment/nonzero-exit behavior and the original overload.

- **Audit A16/A17, 2026-11-14.** An actual snapshot path is distinct from its
  approved path even when the input name lacks the conventional `.approved.`
  marker. Writes replace the actual directory entry atomically and never write
  through an existing link to the approved content. JSON Schema `$ref` siblings
  are additional constraints, not discarded annotations. Parse audits every schema
  branch up front, including unused properties/definitions/alternatives, rejecting
  unknown keywords, invalid local references and cyclic reference graphs. Boolean
  schemas are supported. Annotation and enum values are data, not child schemas.

⚠ 2026-11-14: snapshot naming silently assumed an `approved` substring and the
schema checker followed a `$ref` with an early return, auditing only visited boxes.
Audit A16/A17 found approved-file overwrite and skipped constraints. The distinct
actual path and up-front finite-subset audit replace those assumptions.

- **Nothing above `Data` and `Fixtures`.** The node names types of `Data` (the
  database) and `Fixtures` (the repository paths, the cases, the tolerance table) and
  no type of `Thermo`, `Equilibrium`, `Performance`, `Transport`, `Execution`,
  `Problems` or `Cli`: a harness that knew a species table or a problem would sit above
  the nodes its consumers test, and the root's dependency arrows would stop describing
  the tree.
- **No formula and no tolerance.** A comparison within a tolerance belongs to the
  consumer that derives it or to the fixtures node's table; this node compares bits.

  ⚠ 2026-09-30: was "this node compares bits", now also documents field by field, 1e-9
  relative, in `JsonFieldComparison` → HISTORY.md#compares-bits
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

  ⚠ 2026-09-15: was `StaleKeys` a problem that writes an actual file, now bare keys the
  consumer words → HISTORY.md#stale-keys
- **`*.actual.txt` is CRLF; the repository is LF.** `Problem`'s write
  (`ApprovedSnapshot.cs`, the `File.WriteAllLines` call in the dirty-write branch)
  joins lines with `Environment.NewLine`, CRLF on the Windows platform this tree
  targets (`## Constraints` of the root), while `.gitattributes` keeps every
  committed text file LF. Whoever approves a change by copying the actual file over
  the approved one normalizes the line endings first; the Cli.Tests re-approval of
  2026-09-15 did so.

  ⚠ 2026-09-17: was CRLF "on the Windows platform this tree targets", now CRLF on
  Windows only, LF on Linux → HISTORY.md#crlf-linux
- **A field dump is a caller's opt-in** (2026-09-18, the bits-diagnostics task).
  `BitHash` keeps, alongside the hash it computes, the text of every value added, in
  order: a double round-trip (`"R"`, invariant culture, so a dump reads the same on
  every platform), an int in invariant culture, a bool as `"true"`/`"false"`, a string
  as added — `Fields`, readable before or after `ToHex`, since it is a separate list
  the hash's own disposal does not touch. `ApprovedSnapshot.Problem` takes that list as
  an optional third argument; on a problem it writes each value to its own line of a
  file beside the actual file, named from the actual file's own name and the key
  (sanitized): `<actual-base>.<sanitized key>.fields.txt`. A caller with a composite
  line over more than one `BitHash` can supply a named composite list of diagnostic
  lines. The mechanism accepts caller-provided fields; it does not infer the caller's
  result schema. Consumer adoption and named-field completeness are readiness claims
  in each consumer node's own API/criteria, not a fixed caller inventory here.
  `BitHash.cs`, `ApprovedSnapshot.cs`.

  ⚠ 2026-11-14: was "as of this task only Problems.Tests does" and a list of consumers
  said not to pass fields. That cross-node inventory becomes stale when a consumer
  adopts diagnostics independently (T16). Harness now states the shared mechanism's
  scope; global named-field coverage is verified by the owning consumer checks.
- **One bit record per node, picked in one place** (2026-10-05, owner decision O5).
  `ApprovedSnapshot.RecordPathFor` names `<name>.approved.txt` on every platform, and every
  consumer's Bits level calls it, so the record of a node is one file. The tree's own correctly
  rounded `Exp`, `Log` and `Pow` do not depend on the C runtime of the machine, and the
  Windows and the Linux (WSL2) bits of every node were equal when the pair was dropped. The
  throughput figures keep their platform split, `ApprovedSnapshot.ApprovedPathFor`: a speed
  belongs to a machine.
  ⚠ 2026-10-05: was a Windows and a Linux record for every bit snapshot, picked by
  `ApprovedPathFor`, since the C runtimes of the two platforms did not round the last bit alike
  → HISTORY.md#one-record-2026-10-05
- **A process is run once, here, and its streams are bytes** (2026-10-03, the test
  pyramid). `DotnetProcess.Run` starts `dotnet` on an assembly path with explicit
  arguments, working directory and environment, and returns the exit code and the raw
  bytes of standard output and error, decoded by nobody: the encoding a process writes
  is a fact its caller may be proving. It names no type of the node it runs, so the end-to-end
  facts of `Cli.Tests` and `Docs.Tests` share one runner, and `Protocol.Tests` finds a
  process start by the call in a body, whichever node's fact makes it.
- **One host, CPU only.** `CpuHost` creates one ILGPU context and one CPU accelerator
  and loads the database (with `trans.inp`) and the tolerance table once; it never
  creates a CUDA accelerator.

## Dependencies

- [Data](../../src/Data/API.md) — the database the host loads.
- [Fixtures](../Fixtures/API.md) — the repository paths, the cases, the tolerance table.

Outside the tree: ILGPU 1.5.3 (the CPU accelerator only); the .NET base class library
(`System.Text.Json` for the JSON-document helpers).

## Constraints

- A library assembly (`APThermo.Harness`) referenced by test
  projects only; it references no test framework, so its public surface enters the
  protocol tests node's snapshot like any library's.

  ⚠ 2026-09-25: was `FixtureFamilies.Of` returning `TheoryData`, now `Keys`+`CasesOf`
  (no `xunit` reference here) → HISTORY.md#families-keys
- Every type is a stable type in the root's sense once the test nodes use it: small,
  and named in `API.md`.
- Paths from the repository root through `RepositoryPaths` of the fixtures node.
- The node has no tests node of its own: it is proven through its consumers (the
  acceptance criteria below).

- **`Bits.Differences<T>` reads fields and properties** (2026-09-26, the guards audit of 2026-09-26 (`Audit 5`, the hidden-defect audit's fifth part), F10). It
  compares every public `double` and `int` field and property of `T`. It throws when `T`
  has none, so that a comparison can never be empty by accident.
  - ⚠ 2026-09-26: was properties only, now fields too → HISTORY.md#differences-fields

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No formula, no tolerance, no type of a numerical node, of the front door or of the
  adapter.
- No test of a node's behaviour here: this node is scaffolding.
- No CUDA accelerator.
