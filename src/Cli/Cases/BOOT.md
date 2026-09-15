# BOOT.md — Cli.Cases

## Purpose

Turns a parsed problem document and the sweep's combinations into the library's
problems, solves them, and turns the results back into the output document's cases: the
rocket and equilibrium paths of the `rocket` and `equilibrium` commands. A child node of
`src/Cli` (`AerospacePropellantThermodynamics.Cli.Cases`, compiled into the parent's
assembly, root `BOOT.md`, Constraints, 2026-09-15): it has its own reason to change —
a new field of a case's `inputs` echo, or a new sweep axis — and the rest of the node
reaches it through the case builders and the sweep expansion, never through the
`inputs`-echo writer they share.

Split out of the parent's `## Structure` review of 2026-09-15 (the child-nodes phase,
root `BOOT.md`): `EquilibriumCases`, `RocketCases`, `CaseInputs`, `Sweeps`,
`Propellants`, `Combination` and `CaseOutput` moved here unchanged, `git mv` and a
namespace edit only, no logic touched. `CaseOutput` moved with this cluster, not the
parent's own level, though `StatesCommand` also constructs it directly: `RocketCases`
and `EquilibriumCases` are its two other, primary producers, and every field it carries
mirrors a Cases concept (`Combination`'s ratio and pressure, the library's own mixture
and stations); `Cli.Output` reads it only through its init properties, never through a
method of this node.

## Invariants

- **The order of a batch is the order of its combinations.** `Sweeps.Expand`'s
  Cartesian product (ratio, then chamber pressure, then pressure, then temperature) is
  the one order a rocket or equilibrium batch is built and reported in; `RocketCases`
  and `EquilibriumCases` build one problem per combination, in that order, and place
  results at the same index.
- **The `inputs` echo is written once.** `CaseInputs.Rocket` and `CaseInputs.Equilibrium`
  are the only place a case's `inputs` object is assembled, for both commands; a field
  added to the echo is added here, not duplicated at a call site.
- **A propellant document reads into exactly the library shape its own kind needs.**
  `Propellants.Build(SpeciesDatabase, ReactantPropellant)` returns the library's
  `Propellant` builder result; `Propellants.Build(ElementalPropellant, double)` returns
  an `ElementalMixture` directly, the mass tolerance carried alongside it, never a
  third, mixed shape.

## Dependencies

[Documents](../Documents/API.md)
[Cli](../API.md)
[Data](../../Data/API.md)
[Problems](../../Problems/API.md)
[Thermo](../../Thermo/API.md)
[Performance](../../Performance/API.md)
[Equilibrium](../../Equilibrium/API.md)

Outside the tree: none beyond what the linked nodes already declare in their own
`BOOT.md`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and the root. In addition:

- `CaseOutput` is declared with init properties, all required, so that every
  construction site (`RocketCases.Build`, `EquilibriumCases.Build`, and
  `StatesCommand` at the parent's own level) names every field (the parent's `BOOT.md`,
  F-CL-09).
- No unit conversion: the library's own SI values pass through unchanged; the
  seconds-based specific impulse conversions live in `Cli.Output`, not here.

## Acceptance criteria

- [x] 2026-09-15 — The moved types compile unchanged under
      `AerospacePropellantThermodynamics.Cli.Cases` and every test of
      `tests/Cli.Tests` that exercised them before the move (the sweep, mixture and
      `inputs`-echo cases of `OutputDocumentTests`, `LibraryEqualityTests` and
      `BitSnapshotTests`) passes after it, same count as before the split
      (`tests/Cli.Tests/BOOT.md`, the Bits level).
- [x] 2026-09-15 — The protocol tests node attributes every type of this directory to
      `Cli.Cases` by namespace, not to `Cli` (`Protocol.Tests`,
      `CoverageTests`/`DeclarationTests`, the child-node attribution of 8c3c77f).

## Taboos

- No document parsing: a problem or record document's JSON shape is `Cli.Documents`'.
- No rendering: turning a `CaseOutput` into JSON or CSV cells is `Cli.Output`'s.
- No command-line concern: an option belongs to `Cli.Syntax`'s `CommandOptions`,
  read by the parent's command types, not here.
