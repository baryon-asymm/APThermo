namespace APThermo.Cli.Syntax;

/// <summary>
/// One command: its name, its usage line, the options that apply to it, the formats it supports, and its arity
/// check (the count of positional arguments after the command name; null when the count is acceptable).
/// </summary>
internal sealed record CommandSpec(string Name, string Usage, IReadOnlyList<string> Options, IReadOnlyList<OutputFormat> Formats, Func<int, string?> CheckArity)
{
    public bool Applies(string option) => Options.Contains(option, StringComparer.Ordinal);
}
