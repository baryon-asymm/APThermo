namespace APThermo.Cli.Syntax;

/// <summary>The raw result of the token walk: positionals, the options given (name, raw value or null for a flag), the help flag and the version flag.</summary>
internal sealed record ScannedArguments(IReadOnlyList<string> Positional, IReadOnlyList<(string Name, string? Value)> Options, bool Help, bool Version);
