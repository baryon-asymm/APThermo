# API.md — Cli.Documents

Namespace `AerospacePropellantThermodynamics.Cli.Documents`, compiled into the
`AerospacePropellantThermodynamics.Cli` assembly (root `BOOT.md`, Constraints,
2026-09-15). Every type is `internal`; visible throughout the `Cli` assembly and to
`AerospacePropellantThermodynamics.Cli.Tests`, but the parent and the sibling child
node `Cli.Cases` use only what is named below. `JsonText`, `StrictObject`,
`SweepValues`, `PropellantDocumentReader` and `ProblemPartReader` are this node's own
and may change.

## Readers ✅

```csharp
namespace AerospacePropellantThermodynamics.Cli.Documents;

internal static class ProblemDocumentReader
{
    public static InputDocument Read(string text, string source);
}

internal static class SweepDocumentReader
{
    public static SweepDocument Read(StrictObject sweep, ProblemDocument problem, PropellantDocument propellant);
}

internal static class StateRecordReader
{
    public static IReadOnlyList<(StateRecord Record, RecordSource Source)> Read(IReadOnlyList<(string Source, string Text)> files);
}

internal static class RecordNaming
{
    public static T Named<T>(IReadOnlyList<RecordSource> group, Func<T> solve);
}
```

`ProblemDocumentReader.Read` is the entry point of a rocket or equilibrium problem
document (the parent's `API.md`, "Input document"); `text` is the file's content,
`source` its path, folded into every message. `StateRecordReader.Read` is the entry
point of the `states` command's record files (the parent's `API.md`, "Command line",
the state record shape); every record comes back paired with a `RecordSource`
carrying its position, its label for a message, and its raw JSON for the case's
`inputs` echo. `RecordNaming.Named` runs `solve` and renames a `StateRecordException`
or a `MixtureMassException` (`Problems`') from its index in `group` to the record's own
`Source.Label`, and translates every other `ArgumentException` or
`KeyNotFoundException` the library raises at solve time into a `Cli.InputException`,
unchanged. `SweepDocumentReader.Read` is public here because `ProblemDocumentReader`
calls it; no other node calls it directly.

## Document shapes ✅

```csharp
internal sealed record InputDocument(PropellantDocument Propellant, ProblemDocument Problem, SweepDocument? Sweep, AcceleratorKind? Accelerator);

internal abstract record ProblemDocument;

internal sealed record RocketDocument(
    double ChamberPressure, FlowModel Flow, IReadOnlyList<double> AreaRatios, IReadOnlyList<double> PressureRatios,
    bool Transport, double TemperatureEstimate) : ProblemDocument;

internal sealed record EquilibriumDocument(
    Equilibrium.ProblemKind Kind, double Pressure, double? Temperature, double? Enthalpy, double? Entropy, bool Transport) : ProblemDocument;

internal abstract record PropellantDocument(IReadOnlyList<string> Omit, IReadOnlyList<string>? Only);

internal sealed record ReactantPropellant(
    IReadOnlyList<ReactantDocument> Reactants, double? OxidizerToFuel, IReadOnlyList<string> Omit, IReadOnlyList<string>? Only)
    : PropellantDocument(Omit, Only);

internal sealed record ElementalPropellant(
    IReadOnlyDictionary<string, double> ElementMoles, double? Enthalpy, IReadOnlyList<string> Omit, IReadOnlyList<string>? Only)
    : PropellantDocument(Omit, Only);

internal sealed record ReactantDocument(string Name, ReactantRole Role, double Amount, AmountKind AmountKind)
{
    public double? Temperature { get; init; }
    public CustomReactantDefinition? Custom { get; init; }
}

internal sealed record SweepDocument(
    IReadOnlyList<double>? OxidizerToFuel, IReadOnlyList<double>? ChamberPressure, IReadOnlyList<double>? Pressure, IReadOnlyList<double>? Temperature);

internal sealed record RecordSource(int Index, string Label, JsonElement Raw);
```

`ReactantRole`, `AmountKind` and `CustomReactantDefinition` are `Problems`'; `FlowModel`
is `Performance`'s; `Equilibrium.ProblemKind` is `Equilibrium`'s; `AcceleratorKind` is
`Execution`'s. `Cli.Cases` reads `RocketDocument`, `EquilibriumDocument`,
`SweepDocument`, `ReactantPropellant`, `ElementalPropellant` and `ReactantDocument` by
field, to build the library's problems and mixtures; `ProblemCommand` (the parent's own)
pattern-matches `PropellantDocument` and `ProblemDocument` to their two cases.
