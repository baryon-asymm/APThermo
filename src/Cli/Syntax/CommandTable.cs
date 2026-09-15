using System.Globalization;
using System.Text;
using APThermo.Problems;

namespace APThermo.Cli.Syntax;

/// <summary>
/// The two tables of the command line: every command and every option, with the usage text generated from them
/// (BOOT.md, `## Structure`, F-AR-04) and the numeric defaults read from <see cref="CommandOptions.DefaultThreshold"/>
/// and <see cref="ElementalMixture.DefaultMassTolerance"/> rather than typed a second time.
/// </summary>
internal static class CommandTable
{
    public static readonly IReadOnlyList<OptionSpec> Options =
    [
        new("output", true, "  --output PATH                write the document to PATH instead of standard output\n",
            (o, v) => o with { Output = v }),
        new("format", true, "  --format json|csv            document format (default json; devices: json only)\n",
            (o, v) => o with { Format = OptionValues.ParseFormat(v!) }),
        new("accelerator", true, "  --accelerator auto|cpu|cuda  where to solve (default: the document's engine.accelerator, else auto)\n",
            (o, v) => o with { Accelerator = DocumentWords.ParseAccelerator(v!, null) }),
        new("database", true,
            "  --database DIR               directory with thermo.inp and trans.inp (default: data/ next to the executable, then data/ under the current directory, then the current directory)\n",
            (o, v) => o with { Database = v }),
        new("threshold", true,
            $"  --threshold X                omit mole fractions below X from the compositions (default {CommandOptions.DefaultThreshold.ToString(CultureInfo.InvariantCulture)})\n",
            (o, v) => o with { Threshold = OptionValues.ParseThreshold(v!) }),
        new("mass-tolerance", true,
            $"  --mass-tolerance X           accept element moles whose mass differs from one kilogram by at most X, relative (default {ElementalMixture.DefaultMassTolerance.ToString(CultureInfo.InvariantCulture)})\n",
            (o, v) => o with { MassTolerance = OptionValues.ParseMassTolerance(v!) }),
        new("transport", false, "  --transport                  states: evaluate transport properties at every record\n",
            (o, _) => o with { Transport = true }),
        new("find", true, "  --find TEXT                  species: only names containing TEXT (case-insensitive)\n",
            (o, v) => o with { Find = v }),
    ];

    private static readonly IReadOnlyList<string> SolvingProblemOptions = ["output", "format", "accelerator", "database", "threshold", "mass-tolerance"];

    public static readonly IReadOnlyList<CommandSpec> Commands =
    [
        new("rocket", "  rocket <problem.json>        chamber, throat and exits of a rocket problem document\n",
            SolvingProblemOptions, [OutputFormat.Json, OutputFormat.Csv], OneProblemDocument("rocket")),
        new("equilibrium", "  equilibrium <problem.json>   one equilibrium state (tp, hp or sp) of a problem document\n",
            SolvingProblemOptions, [OutputFormat.Json, OutputFormat.Csv], OneProblemDocument("equilibrium")),
        new("states", "  states <records>...          state records (JSON array, JSON Lines, several files) as one batch\n",
            [.. SolvingProblemOptions, "transport"], [OutputFormat.Json, OutputFormat.Csv],
            count => count >= 1 ? null : "states takes at least one file of records"),
        new("species", "  species                      the species of the database\n",
            ["output", "format", "database", "find"], [OutputFormat.Json, OutputFormat.Csv], NoArguments("species")),
        new("devices", "  devices                      the accelerators this machine offers\n",
            ["output", "format"], [OutputFormat.Json], NoArguments("devices")),
    ];

    public static readonly string Usage = BuildUsage();

    public static readonly IReadOnlyList<string> Names = Commands.Select(c => c.Name).ToList();

    public static CommandSpec? Find(string name) => Commands.FirstOrDefault(c => c.Name == name);

    public static OptionSpec? FindOption(string name) => Options.FirstOrDefault(o => o.Name == name);

    private static Func<int, string?> OneProblemDocument(string command) =>
        count => count == 1 ? null : $"{command} takes exactly one problem document, not {count}";

    private static Func<int, string?> NoArguments(string command) =>
        count => count == 0 ? null : $"{command} takes no argument";

    private static string BuildUsage()
    {
        var text = new StringBuilder("usage: apthermo <command> [arguments] [options]\n\ncommands:\n");
        foreach (var command in Commands)
        {
            text.Append(command.Usage);
        }

        text.Append("\noptions:\n");
        foreach (var option in Options)
        {
            text.Append(option.Usage);
        }

        text.Append("  --help, -h                   this text\n\n");
        text.Append("exit codes: 0 every case ok; 1 a case failed numerically (document written); 2 invalid input; 3 accelerator or infrastructure error\n");
        return text.ToString();
    }
}
