# API.md — Cli.Output

Namespace `AerospacePropellantThermodynamics.Cli.Output`, compiled into the
`AerospacePropellantThermodynamics.Cli` assembly (root `BOOT.md`, Constraints,
2026-09-15). Every type is `internal`; visible throughout the `Cli` assembly and to
`AerospacePropellantThermodynamics.Cli.Tests`, but the parent and the sibling child
node `Cli.Listings` use only what is named below. `JsonOutput`, `ExitCodes`,
`StationFields` and `Cell` are this node's own and may change.

## Writing ✅

```csharp
namespace AerospacePropellantThermodynamics.Cli.Output;

internal static class DocumentWriter
{
    public static ExitCode Write(RunInfo run, IReadOnlyList<CaseOutput> cases, CommandOptions options, TextWriter output);
    public static void Deliver(string text, string? path, TextWriter output);
    public static string Render(Action<Utf8JsonWriter> write);
}

internal static class RunSection
{
    public static void Write(Utf8JsonWriter writer, RunInfo run);
    public static void WriteAccelerator(Utf8JsonWriter writer, AcceleratorInfo accelerator);
}

internal static class CsvOutput
{
    public static string Render(IReadOnlyList<CaseOutput> cases);
    public static string Number(double value);
    public static string Escape(string cell);
}
```

`RunInfo` is the parent's own; `CommandOptions` is `Cli.Syntax`'s; `CaseOutput` is
`Cli.Cases`'; `AcceleratorInfo` is `Execution`'s.

`DocumentWriter.Write` renders `cases` in `options.Format` (JSON through `JsonOutput`,
CSV through `CsvOutput.Render`), delivers the text to `options.Output` or `output`
(`Deliver`), and returns the exit code (`ExitCodes.Of`, this node's own). `Deliver`
and `Render` are the two pieces a listing (`Cli.Listings`) composes itself: `Render`
turns a `Utf8JsonWriter` callback into the document's bytes (the final newline and the
writer options already applied); `Deliver` writes those bytes to a path or to `output`,
raising `Cli.InputException` when the path's directory does not exist (the parent's
`API.md`, "Errors"). `RunSection.Write` writes the `run` object of a case document, and
`RunSection.WriteAccelerator` the accelerator object alone, reused by the `devices`
listing (`Cli.Listings`) for its own `cpu`/`cuda` objects. `CsvOutput.Number` and
`CsvOutput.Escape` are the CSV form's number and quoting rules, reused by the `species`
listing's own CSV form.

## Out of scope

`JsonOutput`'s own JSON form (called only by `DocumentWriter.Write`), the per-station
field projection (`StationFields`, `Cell`), and the exit-code rule's own predicate
(`ExitCodes`) are internal to this node.
