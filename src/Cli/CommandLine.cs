using System.Globalization;
using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

internal enum OutputFormat
{
    Json,
    Csv,
}

/// <summary>The options of one invocation, after parsing; every command checks that the options given apply to it.</summary>
internal sealed record CommandOptions
{
    /// <summary>The reference's print threshold on mole fractions (Fixtures BOOT.md).</summary>
    public const double DefaultThreshold = 5e-6;

    public string? Output { get; init; }

    public OutputFormat Format { get; init; } = OutputFormat.Json;

    public AcceleratorKind? Accelerator { get; init; }

    public string? Database { get; init; }

    public double Threshold { get; init; } = DefaultThreshold;

    public bool Transport { get; init; }

    public string? Find { get; init; }

    /// <summary>The option names given, for the per-command applicability check.</summary>
    public IReadOnlySet<string> Given { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

internal sealed record Invocation(string Command, IReadOnlyList<string> Arguments, CommandOptions Options, bool Help);

/// <summary>Hand-written parsing of the command line (BOOT.md: no dependency for it).</summary>
internal static class CommandLine
{
    public static readonly IReadOnlyList<string> Commands = ["rocket", "equilibrium", "states", "species", "devices"];

    public const string Usage =
        "usage: apthermo <command> [arguments] [options]\n" +
        "\n" +
        "commands:\n" +
        "  rocket <problem.json>        chamber, throat and exits of a rocket problem document\n" +
        "  equilibrium <problem.json>   one equilibrium state (tp, hp or sp) of a problem document\n" +
        "  states <records>...          state records (JSON array, JSON Lines, several files) as one batch\n" +
        "  species                      the species of the database\n" +
        "  devices                      the accelerators this machine offers\n" +
        "\n" +
        "options:\n" +
        "  --output PATH                write the document to PATH instead of standard output\n" +
        "  --format json|csv            document format (default json; devices: json only)\n" +
        "  --accelerator auto|cpu|cuda  where to solve (default: the document's engine.accelerator, else auto)\n" +
        "  --database DIR               directory with thermo.inp and trans.inp (default: data/ next to the executable, then data/ under the current directory, then the current directory)\n" +
        "  --threshold X                omit mole fractions below X from the compositions (default 5e-6)\n" +
        "  --transport                  states: evaluate transport properties at every record\n" +
        "  --find TEXT                  species: only names containing TEXT (case-insensitive)\n" +
        "  --help, -h                   this text\n" +
        "\n" +
        "exit codes: 0 every case ok; 1 a case failed numerically (document written); 2 invalid input; 3 accelerator or infrastructure error\n";

    private static readonly IReadOnlyDictionary<string, string[]> Applicable = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["rocket"] = ["output", "format", "accelerator", "database", "threshold"],
        ["equilibrium"] = ["output", "format", "accelerator", "database", "threshold"],
        ["states"] = ["output", "format", "accelerator", "database", "threshold", "transport"],
        ["species"] = ["output", "format", "database", "find"],
        ["devices"] = ["output", "format"],
    };

    public static Invocation Parse(IReadOnlyList<string> args)
    {
        var positional = new List<string>();
        var given = new HashSet<string>(StringComparer.Ordinal);
        var options = new CommandOptions();
        var help = false;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                help = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            var name = arg[2..];
            string? value = null;
            var equals = name.IndexOf('=');
            if (equals >= 0)
            {
                value = name[(equals + 1)..];
                name = name[..equals];
            }

            if (name == "transport")
            {
                if (value is not null)
                {
                    throw new InputException("option --transport takes no value");
                }

                options = options with { Transport = true };
                given.Add(name);
                continue;
            }

            if (name is not ("output" or "format" or "accelerator" or "database" or "threshold" or "find"))
            {
                throw new InputException($"unknown option '--{name}'; run apthermo --help for the options");
            }

            if (value is null)
            {
                if (i + 1 >= args.Count)
                {
                    throw new InputException($"option --{name} needs a value");
                }

                value = args[++i];
            }

            if (!given.Add(name))
            {
                throw new InputException($"option --{name} was given twice");
            }

            options = name switch
            {
                "output" => options with { Output = value },
                "format" => options with { Format = ParseFormat(value) },
                "accelerator" => options with { Accelerator = ParseAccelerator(value) },
                "database" => options with { Database = value },
                "threshold" => options with { Threshold = ParseThreshold(value) },
                _ => options with { Find = value },
            };
        }

        options = options with { Given = given };
        if (help)
        {
            return new Invocation(positional.Count > 0 ? positional[0] : "", [], options, true);
        }

        if (positional.Count == 0)
        {
            throw new InputException("no command given\n" + Usage);
        }

        var command = positional[0];
        if (!Applicable.TryGetValue(command, out var applicable))
        {
            throw new InputException($"unknown command '{command}'; commands: {string.Join(", ", Commands)}");
        }

        var arguments = positional.Skip(1).ToList();
        switch (command)
        {
            case "rocket" or "equilibrium" when arguments.Count != 1:
                throw new InputException($"{command} takes exactly one problem document, not {arguments.Count}");
            case "states" when arguments.Count == 0:
                throw new InputException("states takes at least one file of records");
            case "species" or "devices" when arguments.Count != 0:
                throw new InputException($"{command} takes no argument");
        }

        foreach (var name in given)
        {
            if (!applicable.Contains(name))
            {
                throw new InputException($"option --{name} does not apply to {command}");
            }
        }

        if (command == "devices" && options.Format == OutputFormat.Csv)
        {
            throw new InputException("devices has no CSV form");
        }

        return new Invocation(command, arguments, options, false);
    }

    private static OutputFormat ParseFormat(string value) => value switch
    {
        "json" => OutputFormat.Json,
        "csv" => OutputFormat.Csv,
        _ => throw new InputException($"unknown format '{value}'; json or csv"),
    };

    private static AcceleratorKind ParseAccelerator(string value) => value switch
    {
        "auto" => AcceleratorKind.Auto,
        "cpu" => AcceleratorKind.Cpu,
        "cuda" => AcceleratorKind.Cuda,
        _ => throw new InputException($"unknown accelerator '{value}'; auto, cpu or cuda"),
    };

    private static double ParseThreshold(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) || !double.IsFinite(threshold) || threshold < 0.0)
        {
            throw new InputException($"the threshold must be a finite non-negative number, not '{value}'");
        }

        return threshold;
    }
}
