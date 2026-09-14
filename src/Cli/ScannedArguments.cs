namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The raw result of the token walk: positionals, the options given (name, raw value or null for a flag), and the help flag.</summary>
internal sealed record ScannedArguments(IReadOnlyList<string> Positional, IReadOnlyList<(string Name, string? Value)> Options, bool Help);
