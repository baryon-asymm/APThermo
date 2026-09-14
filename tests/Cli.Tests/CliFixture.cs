using System.Diagnostics;
using System.Text.Json;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>The result of one invocation: exit code, standard output and standard error.</summary>
public sealed record Run(int Code, string Output, string Error)
{
    public JsonDocument Json() => JsonDocument.Parse(Output);
}

/// <summary>Paths of this node and of the database, a temporary directory, and the in-process and process-level invocations.</summary>
public sealed class CliFixture : IDisposable
{
    public const string CliAssembly = "AerospacePropellantThermodynamics.Cli";

    private readonly Lazy<SpeciesDatabase> _database = new(() => SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp")));

    public CliFixture()
    {
        Temp = Directory.CreateTempSubdirectory("apthermo-tests-").FullName;
    }

    public static string NodeDirectory { get; } = RepositoryPaths.Resolve("tests", "Cli.Tests");

    public static string DocumentsDirectory => Path.Combine(NodeDirectory, "documents");

    public static string SchemasDirectory => Path.Combine(NodeDirectory, "schemas");

    /// <summary>The names of the solvable example documents: every document of the directory that is not a states file.</summary>
    public static IReadOnlyList<string> ProblemDocumentNames() =>
        Directory.GetFiles(DocumentsDirectory, "*.json").Select(p => Path.GetFileName(p)).Where(n => !n.StartsWith("states", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();

    /// <summary>The names of the states example documents of the directory (JSON and JSON Lines), each solvable on its own.</summary>
    public static IReadOnlyList<string> StatesDocumentNames() =>
        Directory.GetFiles(DocumentsDirectory).Select(p => Path.GetFileName(p)!)
            .Where(n => n.StartsWith("states", StringComparison.Ordinal) && (n.EndsWith(".json", StringComparison.Ordinal) || n.EndsWith(".jsonl", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> InvalidDocumentNames() =>
        Directory.GetFiles(Path.Combine(DocumentsDirectory, "invalid"), "*.json").Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToList();

    public string DatabasePath { get; } = RepositoryPaths.Data;

    /// <summary>The committed database, loaded once for the library calls the documents are compared with.</summary>
    public SpeciesDatabase Database => _database.Value;

    public string Temp { get; }

    public string Document(string name) => Path.Combine(DocumentsDirectory, name);

    public string Schema(string name) => Path.Combine(SchemasDirectory, name);

    public string TempFile(string name) => Path.Combine(Temp, name);

    /// <summary>In-process through the entry point.</summary>
    public Run Invoke(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
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

[CollectionDefinition(Name)]
public sealed class CliCollection : ICollectionFixture<CliFixture>
{
    public const string Name = "cli";
}
