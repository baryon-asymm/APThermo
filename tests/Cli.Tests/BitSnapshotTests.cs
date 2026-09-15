using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>
/// Bits: the output of every example that runs is unchanged since it was recorded (BOOT.md, the Bits row); a tripwire
/// against unnoticed change, not a contract (like the surface snapshot, AGENTS.md §13). A decomposition, a renaming or a
/// reordering of code moves no line; a legitimate change of the documents does, and is reviewed and re-approved with it.
/// </summary>
[Collection(CliCollection.Name)]
public sealed class BitSnapshotTests(CliFixture fixture)
{
    private static readonly string ApprovedPath = Path.Combine(CliFixture.NodeDirectory, "Bits.approved.txt");

    [Fact]
    public void Every_example_gives_the_recorded_output()
    {
        var snapshot = ApprovedSnapshot.Load(ApprovedPath);
        var problems = new List<string>();
        foreach (var example in BitExamples.ComputeAll(fixture))
        {
            var problem = snapshot.Problem(example.Name, $"{example.JsonSha256}\t{example.CsvSha256}");
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        Assert.True(problems.Count == 0, $"{problems.Count} example(s) no longer give the recorded output:\n" + string.Join("\n", problems));
    }
}

/// <summary>One example's recorded output: the SHA-256 of its JSON document without `run`, and of its CSV text.</summary>
internal sealed record BitExample(string Name, string JsonSha256, string CsvSha256);

/// <summary>
/// Every example the Bits level covers (BOOT.md, the Bits row): the problem and states documents of documents/, the
/// problem and record examples of the Cli API read from API.md, and the species listing. Gathered, never typed, so the
/// list cannot fall behind documents/ or API.md.
/// </summary>
internal static class BitExamples
{
    public static IReadOnlyList<BitExample> ComputeAll(CliFixture fixture)
    {
        var examples = new List<BitExample>();
        foreach (var name in CliFixture.ProblemDocumentNames())
        {
            var command = name.StartsWith("rocket", StringComparison.Ordinal) ? "rocket" : "equilibrium";
            examples.Add(FromArgs(fixture, name, fixture.Solving(command, fixture.Document(name)), fixture.Solving(command, fixture.Document(name), "--format", "csv")));
        }

        foreach (var name in CliFixture.StatesDocumentNames())
        {
            examples.Add(FromArgs(fixture, name, fixture.Solving("states", fixture.Document(name)), fixture.Solving("states", fixture.Document(name), "--format", "csv")));
        }

        examples.AddRange(ApiProblemExamples(fixture));
        examples.AddRange(ApiRecordExamples(fixture));
        examples.Add(FromArgs(fixture, "species",
            ["species", "--database", fixture.DatabasePath], ["species", "--database", fixture.DatabasePath, "--format", "csv"]));
        return examples.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>The `## Input document` fences of the Cli API, each solved through the command its own problem type names.</summary>
    private static IEnumerable<BitExample> ApiProblemExamples(CliFixture fixture)
    {
        var api = File.ReadAllText(RepositoryPaths.Resolve("src", "Cli", "API.md"));
        var inputs = CliFixture.JsonFencesOf(api, "## Input document ✅");
        for (var i = 0; i < inputs.Count; i++)
        {
            var name = $"API.md input example {i}";
            var isRocket = ProblemDocumentReader.Read(inputs[i], name).Problem is RocketDocument;
            var path = fixture.TempFile($"bits-input-{i}.json");
            File.WriteAllText(path, inputs[i]);
            var command = isRocket ? "rocket" : "equilibrium";
            yield return FromArgs(fixture, name, fixture.Solving(command, path), fixture.Solving(command, path, "--format", "csv"));
        }
    }

    /// <summary>The `## Command line` state-record fences of the Cli API, each solved through `states`.</summary>
    private static IEnumerable<BitExample> ApiRecordExamples(CliFixture fixture)
    {
        var api = File.ReadAllText(RepositoryPaths.Resolve("src", "Cli", "API.md"));
        var records = CliFixture.JsonFencesOf(api, "## Command line ✅");
        for (var i = 0; i < records.Count; i++)
        {
            var name = $"API.md record example {i}";
            var path = fixture.TempFile($"bits-record-{i}.json");
            File.WriteAllText(path, records[i]);
            yield return FromArgs(fixture, name, fixture.Solving("states", path), fixture.Solving("states", path, "--format", "csv"));
        }
    }

    private static BitExample FromArgs(CliFixture fixture, string name, string[] jsonArgs, string[] csvArgs)
    {
        var json = fixture.Invoke(jsonArgs);
        Assert.True(json.Code is 0 or 1, $"{name}: exit code {json.Code}: {json.Error}");
        var csv = fixture.Invoke(csvArgs);
        Assert.True(csv.Code is 0 or 1, $"{name}: exit code {csv.Code} (csv): {csv.Error}");
        return new BitExample(name, Sha256(WithoutRun(json.Output)), Sha256(csv.Output));
    }

    private static string WithoutRun(string json)
    {
        var document = JsonNode.Parse(json)!.AsObject();
        document.Remove("run");
        return document.ToJsonString();
    }

    private static string Sha256(string text) => new BitHash().Add(text).ToHex();
}
