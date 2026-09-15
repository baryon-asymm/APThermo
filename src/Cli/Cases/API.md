# API.md — Cli.Cases

Namespace `AerospacePropellantThermodynamics.Cli.Cases`, compiled into the
`AerospacePropellantThermodynamics.Cli` assembly (root `BOOT.md`, Constraints,
2026-09-15). Every type is `internal`; visible throughout the `Cli` assembly and to
`AerospacePropellantThermodynamics.Cli.Tests`, but the parent and the sibling child
node `Cli.Output` use only what is named below. `CaseInputs` is this node's own and may
change.

## Case builders ✅

```csharp
namespace AerospacePropellantThermodynamics.Cli.Cases;

internal static class Sweeps
{
    public static IReadOnlyList<Combination> Expand(SweepDocument? sweep);
}

internal static class Propellants
{
    public static Propellant Build(SpeciesDatabase database, ReactantPropellant document);
    public static ElementalMixture Build(ElementalPropellant document, double massTolerance);
}

internal static class RocketCases
{
    public static IReadOnlyList<CaseOutput> Build(
        Solver solver, IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<Combination> combinations,
        RocketDocument document, double? ownRatio);
}

internal static class EquilibriumCases
{
    public static IReadOnlyList<CaseOutput> Build(
        Solver solver, IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<Combination> combinations,
        EquilibriumDocument document, double? ownRatio);
}
```

`SweepDocument`, `RocketDocument`, `EquilibriumDocument`, `ReactantPropellant` and
`ElementalPropellant` are `Cli.Documents`'. `Propellant`, `Solver` and `ElementalMixture`
are `Problems`'; `SpeciesDatabase` is `Data`'s. `RocketCases.Build` and
`EquilibriumCases.Build` solve the batch (`Solver.Solve`) and return one `CaseOutput`
per combination, in the combinations' own order; `ownRatio` is the propellant's own
oxidizer-to-fuel ratio, used for a combination whose own ratio is null (no sweep over
it).

## Case shapes ✅

```csharp
internal sealed record Combination(double? OxidizerToFuel, double? ChamberPressure, double? Pressure, double? Temperature);

internal sealed record CaseOutput
{
    public required int Index { get; init; }
    public required JsonNode Inputs { get; init; }
    public required CaseStatus Status { get; init; }
    public required ElementalMixture Mixture { get; init; }
    public required double MixtureMass { get; init; }
    public required IReadOnlyList<string> Species { get; init; }
    public required IReadOnlyList<Station> Stations { get; init; }
}
```

`CaseStatus` is `Thermo`'s; `Station` is `Problems`' (the parent's `API.md`, "Output
document"). `Cli.Output` reads every field of `CaseOutput` to render
a case; the parent's `StatesCommand` constructs `CaseOutput` directly for the `states`
command, one instance per record, outside a `Sweeps`/`Combination` batch.
