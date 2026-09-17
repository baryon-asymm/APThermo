# API.md — Cli.Listings

Namespace `APThermo.Cli.Listings`, compiled into the
`APThermo.Cli` assembly (root `BOOT.md`, Constraints,
2026-09-15). Every type is `internal`; visible throughout the `Cli` assembly and to
`APThermo.Cli.Tests`, but the parent uses only what is named
below. `DeviceProbe` and `DeviceReport` are this node's own and may change.

## Species ✅

```csharp
namespace APThermo.Cli.Listings;

internal sealed record SpeciesRow
{
    public required string Name { get; init; }
    public required string Section { get; init; }
    public required string Phase { get; init; }
    public required IReadOnlyList<ElementCount> Formula { get; init; }
    public required double MolarMass { get; init; }
    public required double FormationEnthalpy { get; init; }
    public double? TemperatureLow { get; init; }
    public double? TemperatureHigh { get; init; }
    public double? AssignedTemperature { get; init; }
    public required bool TransportData { get; init; }

    public static SpeciesRow From(Species species, SpeciesDatabase database);
}

internal static class SpeciesListing
{
    public static string Json(RunInfo run, IReadOnlyList<SpeciesRow> rows);
    public static string Csv(IReadOnlyList<SpeciesRow> rows);
}
```

`Species`, `SpeciesDatabase` and `ElementCount` are `Data`'s; `RunInfo` is the parent's
own. The parent's `SpeciesCommand` builds one `SpeciesRow` per matching species with
`SpeciesRow.From`, then renders the rows with `SpeciesListing.Json` or `.Csv` (the
parent's `API.md`, "Command line", the `species` command).

## Devices ✅

```csharp
internal static class DeviceListing
{
    public static ExitCode Execute(Invocation invocation, TextWriter output);
}
```

`Invocation` is `Cli.Syntax`'s. `DeviceListing.Execute` is the `devices` command
in full: it probes the machine, renders the `cpu` and `cuda` accelerator objects (the
parent's `API.md`, "Output document", `run.accelerator`'s own fields, and
`cudaForbidden` next to them), delivers the document, and always returns
`ExitCode.Ok` (probing never fails the command; a bound or unavailable CUDA device is
reported, not refused). `CommandRegistry` (the parent's own) maps the `devices` command
name to this method.
