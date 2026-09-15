# API.md — Cli.Syntax

Namespace `AerospacePropellantThermodynamics.Cli.Syntax`, compiled into the
`AerospacePropellantThermodynamics.Cli` assembly (root `BOOT.md`, Constraints,
2026-09-15: a child node without a project of its own compiles into its nearest
ancestor's). Every type is `internal`; visible throughout the `Cli` assembly and to
`AerospacePropellantThermodynamics.Cli.Tests`, but the parent, the sibling child nodes
and the tests node use only what is named below. Everything else (`ArgumentScanner`,
`ScannedArguments`, `CommandSpec`, `CommandTable.Options`, `CommandTable.Find`,
`CommandTable.FindOption`, `OptionSpec`, `OptionValues`) is this node's own and may
change.

## Command line ✅

```csharp
namespace AerospacePropellantThermodynamics.Cli.Syntax;

internal static class CommandLine
{
    public static Invocation Parse(IReadOnlyList<string> args);
}

internal static class CommandTable
{
    public static readonly IReadOnlyList<string> Names;
    public static readonly string Usage;
}

internal sealed record Invocation(string Command, IReadOnlyList<string> Arguments, CommandOptions Options, bool Help);
```

`CommandLine.Parse` throws `Cli.InputException` (the parent's own; every message it
carries is documented in the parent's `API.md`, "Command line" and "Errors"): an
unknown command or option, a wrong argument count, an option that does not apply to the
command, or a bad option value. `CommandTable.Names` is every command name, in table
order; `CommandTable.Usage` is the full `--help` text (`Program` writes it verbatim).

## Options ✅

```csharp
internal sealed record CommandOptions
{
    public const double DefaultThreshold = 5e-6;

    public string? Output { get; init; }
    public OutputFormat Format { get; init; } = OutputFormat.Json;
    public AcceleratorKind? Accelerator { get; init; }
    public string? Database { get; init; }
    public double Threshold { get; init; } = DefaultThreshold;
    public double MassTolerance { get; init; } = ElementalMixture.DefaultMassTolerance;
    public bool Transport { get; init; }
    public string? Find { get; init; }
    public IReadOnlySet<string> Given { get; init; }
}

internal enum OutputFormat { Json, Csv }
```

Every field of `CommandOptions` is the parsed, validated value of the option of the
same name (the parent's `API.md`, "Command line"); `Given` is the set of option names
the invocation actually carried, for a command's own applicability check, which this
node already performed once. `AcceleratorKind` is `Execution`'s; `ElementalMixture` is
`Problems`'.

## Out of scope

The tables' own shape (`CommandSpec`, `OptionSpec`) and the token walk
(`ArgumentScanner`, `ScannedArguments`) are internal to this node: a command or an
option is added to `CommandTable`, never assembled by a caller from these pieces.
