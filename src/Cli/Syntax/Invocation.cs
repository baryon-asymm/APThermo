namespace AerospacePropellantThermodynamics.Cli.Syntax;

internal sealed record Invocation(string Command, IReadOnlyList<string> Arguments, CommandOptions Options, bool Help);
