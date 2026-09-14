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
  first field; a key missing from the approved file, a differing line and an approved
  key that no run produced are each a problem naming the key and how to approve; on a
  problem the actual lines are written next to the approved file as `*.actual.txt`
  (git-ignored), never over it.
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
- [ ] Nothing moved: every Bits level of the tree green with its approved file byte
      for byte unchanged, every kernel-equality and batch bit-equality test green. The
      node seen red through its consumers once, each mutation alone: `Bits.Same` made
      to answer false (every bit-equality test red); a double added to `BitHash` in
      big-endian order (every Bits level red); a line for a fixture that does not exist
      added to an approved file (that Bits level red, naming the key).

## Taboos

- No formula, no tolerance, no type of a numerical node, of the front door or of the
  adapter.
- No test of a node's behaviour here: this node is scaffolding.
- No CUDA accelerator.
