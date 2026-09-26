using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Problems;

namespace APThermo.Cli.Tests;

/// <summary>The result of one invocation: exit code, standard output and standard error.</summary>
internal sealed record Run(int Code, string Output, string Error)
{
    /// <summary>Json.</summary>
    public JsonDocument Json() => JsonDocument.Parse(Output);
}

/// <summary>
/// Paths of this node and of the database, a temporary directory, and the in-process and process-level invocations,
/// shared by every test class of this node. Held as the single <see cref="Shared"/> instance rather than through
/// <c>ICollectionFixture&lt;T&gt;</c>: xUnit requires a class fixture's consuming constructor to be the class's
/// single public constructor, which would force this internal-only helper public for no reason a consumer outside
/// this node has (CA1515). The classes that shared one <c>CliFixture</c> through the "cli"
/// <c>ICollectionFixture&lt;T&gt;</c> collection at 8375261 still run sequentially relative to each other, tagged
/// <c>[Collection(CollectionName)]</c>, a string-named xUnit collection that needs no public
/// <c>[CollectionDefinition]</c> class to exist.
/// </summary>
internal sealed class CliFixture : IDisposable
{
    /// <summary>The xUnit collection name every consuming class of this node is tagged with.</summary>
    public const string CollectionName = "cli";

    /// <summary>The command line's assembly name, for <see cref="CliAssemblyPath"/> and <see cref="InvokeProcess"/>.</summary>
    public const string CliAssembly = "APThermo.Cli";

    /// <summary>The one instance every test class of this node shares.</summary>
    public static readonly CliFixture Shared = new();

    private readonly Lazy<SpeciesDatabase> _database = new(() => SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp")));

    /// <summary>Creates the fixture's own temporary directory, deleted by <see cref="Dispose"/>.</summary>
    public CliFixture()
    {
        Temp = Directory.CreateTempSubdirectory("apthermo-tests-").FullName;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    /// <summary>This node's own directory, the root of <see cref="DocumentsDirectory"/>.</summary>
    public static string NodeDirectory { get; } = RepositoryPaths.Resolve("tests", "Cli.Tests");

    /// <summary>The example documents directory, under this node.</summary>
    public static string DocumentsDirectory => Path.Combine(NodeDirectory, "documents");

    /// <summary>The names of the solvable example documents: every document of the directory that is not a states file.</summary>
    public static IReadOnlyList<string> ProblemDocumentNames() =>
        [.. Directory.GetFiles(DocumentsDirectory, "*.json").Select(p => Path.GetFileName(p)).Where(n => !n.StartsWith("states", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    /// <summary>The names of the states example documents of the directory (JSON and JSON Lines), each solvable on its own.</summary>
    public static IReadOnlyList<string> StatesDocumentNames() =>
        [.. Directory.GetFiles(DocumentsDirectory).Select(p => Path.GetFileName(p)!)
            .Where(n => n.StartsWith("states", StringComparison.Ordinal) && (n.EndsWith(".json", StringComparison.Ordinal) || n.EndsWith(".jsonl", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];

    /// <summary>Invalid document names.</summary>
    public static IReadOnlyList<string> InvalidDocumentNames() =>
        [.. Directory.GetFiles(Path.Combine(DocumentsDirectory, "invalid"), "*.json").Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal)];

    /// <summary>The committed database's own directory, for `--database`.</summary>
    public string DatabasePath { get; } = RepositoryPaths.Data;

    /// <summary>The committed database, loaded once for the library calls the documents are compared with.</summary>
    public SpeciesDatabase Database => _database.Value;

    /// <summary>
    /// The mass in grams a composition weighs with the database's atomic weights (<c>Solver.MassOf</c>, kg to g): what
    /// a refusal message reports, derived here so a test never types the number the library also computes (the
    /// review's F-TF-12: "2000.03 g" was typed in two files).
    /// </summary>
    public double GramsOf(IReadOnlyDictionary<string, double> elementMoles)
    {
        using var solver = Solver.Create(Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        return solver.MassOf(ElementalMixture.Create(elementMoles)) * 1000.0;
    }

    /// <summary>A `composition` or `elementMoles` object already navigated to, as element moles per kilogram.</summary>
    public static IReadOnlyDictionary<string, double> CompositionOf(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<double>(), StringComparer.Ordinal);
    }

    /// <summary>
    /// The one camel-case rule of this node: a field or status name of the library into the document's own spelling,
    /// written independently of the adapter's <c>Names.Camel</c>. An L2 or schema check that instead called the
    /// adapter's own rule to derive its expectation could never see that rule go wrong, since the actual document and
    /// the expected name would always agree by construction; this is the check's independent half. Both
    /// <c>LibraryEqualityTests</c> and <c>OutputDocumentTests</c> read this one method rather than each carrying its
    /// own copy or, as before, calling into the adapter.
    /// </summary>
    public static string Camel(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>This fixture's own temporary directory, an empty directory with no `data/` beside it.</summary>
    public string Temp { get; }

    /// <summary>Document.</summary>
    public static string Document(string name) => Path.Combine(DocumentsDirectory, name);

    /// <summary>Schema text.</summary>
    public static string SchemaText(string name) => SchemaResources.TryGet(name, out var text) ? text : throw new ArgumentException($"unknown schema '{name}'");

    /// <summary>Temp file.</summary>
    public string TempFile(string name) => Path.Combine(Temp, name);

    /// <summary>In-process through the entry point.</summary>
    public static Run Invoke(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return new Run(code, output.ToString(), error.ToString());
    }

    /// <summary>The arguments of a solving command on the committed database and the CPU accelerator.</summary>
    public string[] Solving(string command, string document, params string[] more) =>
        [command, document, "--database", DatabasePath, "--accelerator", "cpu", .. more];

    /// <summary>The document a command produces on the CPU accelerator, parsed, with its exit code.</summary>
    public (int Code, JsonDocument Document, string Error) Produce(string command, string documentName, params string[] more)
    {
        var run = Invoke(Solving(command, Document(documentName), more));
        Assert.True(run.Code is 0 or 1, $"exit code {run.Code}: {run.Error}");
        return (run.Code, run.Json(), run.Error);
    }

    /// <summary>As a separate process: dotnet on the command line's assembly, so that exit codes and the standard streams are real.</summary>
    public Run InvokeProcess(IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Temp,
        };
        start.ArgumentList.Add(CliAssemblyPath());
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                start.Environment[name] = value;
            }
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new Run(process.ExitCode, output.Result, error.Result);
    }

    /// <summary>The command line's assembly next to this test assembly when its runtime configuration was copied there, else its own build output.</summary>
    public static string CliAssemblyPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, CliAssembly + ".dll");
        if (File.Exists(local) && File.Exists(Path.Combine(AppContext.BaseDirectory, CliAssembly + ".runtimeconfig.json")))
        {
            return local;
        }

        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ? "Release" : "Debug";
        var own = RepositoryPaths.Resolve("src", "Cli", "bin", configuration, "net10.0", CliAssembly + ".dll");
        return File.Exists(own) ? own : throw new FileNotFoundException("the command line's assembly was not built", own);
    }

    /// <summary>The json fences of one second-level section of a markdown document.</summary>
    public static IReadOnlyList<string> JsonFencesOf(string markdown, string heading)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var start = lines.FindIndex(l => l.Trim() == heading);
        Assert.True(start >= 0, $"no heading '{heading}'");
        var fences = new List<string>();
        var body = new List<string>();
        var inside = false;
        for (var i = start + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!inside && line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (!inside && line.Trim() == "```json")
            {
                inside = true;
                body.Clear();
                continue;
            }

            if (inside && line.Trim() == "```")
            {
                inside = false;
                fences.Add(string.Join("\n", body));
                continue;
            }

            if (inside)
            {
                body.Add(line);
            }
        }

        return fences;
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Temp, true);
        }
        catch (IOException)
        {
            // a file of a failed test may still be open; the temporary directory is cleaned by the system
        }
    }
}
