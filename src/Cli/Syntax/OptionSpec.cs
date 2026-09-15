namespace AerospacePropellantThermodynamics.Cli.Syntax;

/// <summary>One command-line option: its name, whether it takes a value, its usage line, and how it folds into <see cref="CommandOptions"/>.</summary>
internal sealed record OptionSpec(string Name, bool TakesValue, string Usage, Func<CommandOptions, string?, CommandOptions> Apply);
