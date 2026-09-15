namespace APThermo.Cli.Syntax;

/// <summary>The token walk: --name, --name=value, --help/-h, positionals, a repeated option; knows nothing of commands.</summary>
internal static class ArgumentScanner
{
    public static ScannedArguments Scan(IReadOnlyList<string> args, IReadOnlyList<OptionSpec> known)
    {
        var positional = new List<string>();
        var given = new List<(string Name, string? Value)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var help = false;
        var version = false;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                help = true;
                continue;
            }

            if (arg == "--version")
            {
                version = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            i = ReadOption(args, i, known, seen, given);
        }

        return new ScannedArguments(positional, given, help, version);
    }

    private static int ReadOption(IReadOnlyList<string> args, int i, IReadOnlyList<OptionSpec> known, HashSet<string> seen, List<(string Name, string? Value)> given)
    {
        var arg = args[i];
        var name = arg[2..];
        string? value = null;
        var equals = name.IndexOf('=');
        if (equals >= 0)
        {
            value = name[(equals + 1)..];
            name = name[..equals];
        }

        var spec = known.FirstOrDefault(o => o.Name == name) ?? throw new InputException($"unknown option '--{name}'; run apthermo --help for the options");
        if (!spec.TakesValue)
        {
            if (value is not null)
            {
                throw new InputException($"option --{name} takes no value");
            }
        }
        else if (value is null)
        {
            if (i + 1 >= args.Count)
            {
                throw new InputException($"option --{name} needs a value");
            }

            value = args[++i];
        }

        if (!seen.Add(name))
        {
            throw new InputException($"option --{name} was given twice");
        }

        given.Add((name, value));
        return i;
    }
}
