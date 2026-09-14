namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// Hand-written parsing of the command line (BOOT.md: no dependency for it): scan, look up the command, check its
/// arity, fold the options into <see cref="CommandOptions"/>, then check that every option given applies to the
/// command and that the command supports the requested format. Keeps <see cref="Parse"/>, <see cref="Usage"/> and
/// <see cref="Commands"/> as the tests node knows them.
/// </summary>
internal static class CommandLine
{
    public static IReadOnlyList<string> Commands { get; } = CommandTable.Commands.Select(c => c.Name).ToList();

    public static string Usage => CommandTable.Usage;

    public static Invocation Parse(IReadOnlyList<string> args)
    {
        var scanned = ArgumentScanner.Scan(args, CommandTable.Options);
        var options = Fold(scanned.Options);
        if (scanned.Help)
        {
            return new Invocation(scanned.Positional.Count > 0 ? scanned.Positional[0] : "", [], options, true);
        }

        if (scanned.Positional.Count == 0)
        {
            throw new InputException("no command given\n" + Usage);
        }

        var command = scanned.Positional[0];
        var spec = CommandTable.Find(command) ?? throw new InputException($"unknown command '{command}'; commands: {string.Join(", ", Commands)}");
        var arguments = scanned.Positional.Skip(1).ToList();
        if (spec.CheckArity(arguments.Count) is { } arityError)
        {
            throw new InputException(arityError);
        }

        CheckApplicability(spec, command, options);
        return new Invocation(command, arguments, options, false);
    }

    /// <summary>Folds every scanned option into <see cref="CommandOptions"/>, regardless of command: a bad value is reported before the command is even looked at.</summary>
    private static CommandOptions Fold(IReadOnlyList<(string Name, string? Value)> given)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var options = new CommandOptions();
        foreach (var (name, value) in given)
        {
            options = CommandTable.FindOption(name)!.Apply(options, value);
            names.Add(name);
        }

        return options with { Given = names };
    }

    private static void CheckApplicability(CommandSpec spec, string command, CommandOptions options)
    {
        foreach (var name in options.Given)
        {
            if (!spec.Applies(name))
            {
                throw new InputException($"option --{name} does not apply to {command}");
            }
        }

        if (!spec.Formats.Contains(options.Format))
        {
            throw new InputException($"{command} has no {(options.Format == OutputFormat.Csv ? "CSV" : "JSON")} form");
        }
    }
}
