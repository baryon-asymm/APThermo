# BOOT.md — Cli.Listings

## Purpose

The rendering of the two listing commands, `species` and `devices`: the database's
species flattened into rows and the accelerators this machine offers, each in JSON or
CSV (`species`) or JSON only (`devices`). A child node of `src/Cli`
(`AerospacePropellantThermodynamics.Cli.Listings`, compiled into the parent's assembly,
root `BOOT.md`, Constraints, 2026-09-15): it has its own reason to change — a species
field, a device field — and the rest of the node reaches it through
`SpeciesListing.Json`/`Csv` and `DeviceListing.Execute`, never through the accelerator
probe it renders.

Split out of the parent's `## Structure` review of 2026-09-15 (the child-nodes phase,
root `BOOT.md`): `SpeciesRow`, `SpeciesListing`, `DeviceProbe`, `DeviceReport` and
`DeviceListing` moved here unchanged, `git mv` and a namespace edit only, no logic
touched. `SpeciesCommand` (the `species` command's own read-filter-deliver sequence)
stayed at the parent's own level, alongside `ProblemCommand` and `StatesCommand`: it is
a composition root, holding no formula of its own.

## Invariants

- **A species is flattened once.** `SpeciesRow.From` is the only place a `Data` node's
  `Species` becomes the fields both document forms of `species` read; a field added to
  the row reaches both forms without a second read of the database (the parent's
  `BOOT.md`, F-CL-08).
- **The devices probe never throws for a missing CUDA device.** `DeviceProbe.Run`
  always returns a `DeviceReport`: the CPU accelerator always created, the CUDA one
  `null` with its own message and paths tried when it could not be bound
  (`AcceleratorUnavailableException` and `InvalidOperationException`, both `Execution`'s,
  caught here and turned into the report's fields, never propagated).
- **`devices` is `Execution.Engine.CudaForbidden` read once**, next to `cpu` and `cuda`,
  so a run under `APTHERMO_NO_CUDA=1` is visible on the listing itself.

## Dependencies

[Output](../Output/API.md)
[Syntax](../Syntax/API.md)
[Cli](../API.md)
[Data](../../Data/API.md)
[Execution](../../Execution/API.md)

Outside the tree: none beyond what the linked nodes already declare in their own
`BOOT.md`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and the root. In addition:

- `SpeciesRow` is declared with init properties, all but the two temperature-interval
  pairs required, mirroring `CaseOutput`'s reason (the parent's `BOOT.md`, F-CL-09):
  a species with polynomial intervals carries `TemperatureLow`/`TemperatureHigh`, one
  without carries `AssignedTemperature`, never both.
- No delivery of its own: both listings render text and hand it to `Cli.Output`'s
  `DocumentWriter.Deliver`, which the parent's command types (`SpeciesCommand`) or this
  node's own `DeviceListing.Execute` call.

## Acceptance criteria

- [x] 2026-09-15 — The moved types compile unchanged under
      `AerospacePropellantThermodynamics.Cli.Listings` and every test of
      `tests/Cli.Tests` that exercised them before the move (the `species` and
      `devices` cases of `OutputDocumentTests`, `CsvTests` and `BitSnapshotTests`)
      passes after it, same count as before the split
      (`tests/Cli.Tests/BOOT.md`, the Bits level).
- [x] 2026-09-15 — The protocol tests node attributes every type of this directory to
      `Cli.Listings` by namespace, not to `Cli` (`Protocol.Tests`,
      `CoverageTests`/`DeclarationTests`, the child-node attribution of 8c3c77f).

## Taboos

- No solving and no problem document: the listing commands read the database or probe
  the accelerators directly, never through `Cli.Documents` or `Cli.Cases`.
- No command dispatch: `CommandRegistry` (the parent's own) still maps `species` and
  `devices` to `SpeciesCommand` (the parent's own) and this node's `DeviceListing`.
