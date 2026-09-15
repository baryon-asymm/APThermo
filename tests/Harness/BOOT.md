# BOOT.md — Harness

## Purpose

The scaffolding the test nodes share: one CPU accelerator with the committed database
and the tolerance table, bit-for-bit comparison, bit hashes and the approval of their
snapshot files, and the grouping of fixture cases into families for batch tests. It
holds no formula and names no type of the nodes its consumers test, so it can move no
result.

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
  consumers word it themselves and, until this repair task, four never called it at all.
  Found by the repair review of 2026-09-15 reading the code against the claim.
- **One host, CPU only.** `CpuHost` creates one ILGPU context and one CPU accelerator
  and loads the database (with `trans.inp`) and the tolerance table once; it never
  creates a CUDA accelerator.

## Dependencies

- [Data](../../src/Data/API.md) — the database the host loads.
- [Fixtures](../Fixtures/API.md) — the repository paths, the cases, the tolerance table.

Outside the tree: ILGPU 1.5.3 (the CPU accelerator only).

## Constraints

- A library assembly (`AerospacePropellantThermodynamics.Harness`) referenced by test
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
      `Every_fixture_with_transport_gives_the_recorded_bits` red, one problem naming
      the key - "tests/Fixtures/cases/rocket/does-not-exist_pc1MPa_shiftingEquilibrium.json:
      recorded in the approved snapshot, but no such fixture is run with transport"
      (`ApprovedSnapshot.StaleKeys`; wired by `Transport.Tests`, `Equilibrium.Tests`
      and, since 2026-09-15, `Cli.Tests` (this repair's R-Cli.Tests-1:
      `Every_example_gives_the_recorded_output` now calls `StaleKeys` too, worded the
      way `Problem`'s own messages are). `Thermo.Tests`, `Performance.Tests` and
      `Problems.Tests` still key their theories from the fixture directory alone and
      do not check for an orphaned approved line - a gap recorded here rather than
      closed silently, since fixing it edits three foreign nodes' own test code,
      outside this task).

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

## Taboos

- No formula, no tolerance, no type of a numerical node, of the front door or of the
  adapter.
- No test of a node's behaviour here: this node is scaffolding.
- No CUDA accelerator.
