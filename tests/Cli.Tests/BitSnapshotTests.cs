using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Fixtures;

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
    private static readonly string ActualPath = Path.Combine(CliFixture.NodeDirectory, "Bits.actual.txt");

    [Fact]
    public void Every_example_gives_the_recorded_output()
    {
        var approved = BitFile.Read(ApprovedPath);
        var actual = BitExamples.ComputeAll(fixture);
        var mismatches = Mismatches(approved, actual);
        if (mismatches.Count == 0)
        {
            if (File.Exists(ActualPath))
            {
                File.Delete(ActualPath);
            }

            return;
        }

        BitFile.Write(ActualPath, actual);
        Assert.Fail(
            $"{mismatches.Count} example(s) moved from {ApprovedPath}:\n{string.Join("\n", mismatches)}\n" +
            $"A decomposition, a renaming or a reordering of code must move no line here; if the documents themselves " +
            $"changed on purpose, in the same commit as that change, review {ActualPath} and copy it over {ApprovedPath}.");
    }

    private static IReadOnlyList<string> Mismatches(IReadOnlyDictionary<string, BitExample> approved, IReadOnlyList<BitExample> actual)
    {
        var messages = new List<string>();
        foreach (var example in actual)
        {
            if (!approved.TryGetValue(example.Name, out var expected))
            {
                messages.Add($"  {example.Name}: absent from Bits.approved.txt");
            }
            else if (expected.JsonSha256 != example.JsonSha256 || expected.CsvSha256 != example.CsvSha256)
            {
                messages.Add($"  {example.Name}: json {(expected.JsonSha256 == example.JsonSha256 ? "matches" : "differs")}, " +
                             $"csv {(expected.CsvSha256 == example.CsvSha256 ? "matches" : "differs")}");
            }
        }

        return messages;
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
            var isRocket = InputDocuments.ReadProblem(inputs[i], name).Problem is RocketDocument;
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

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>The line format of Bits.approved.txt: one line per example, its name, then its JSON and CSV SHA-256, tab-separated.</summary>
internal static class BitFile
{
    public static IReadOnlyDictionary<string, BitExample> Read(string path)
    {
        var map = new Dictionary<string, BitExample>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return map;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            map[parts[0]] = new BitExample(parts[0], parts[1], parts[2]);
        }

        return map;
    }

    public static void Write(string path, IReadOnlyList<BitExample> examples) =>
        File.WriteAllLines(path, examples.Select(e => $"{e.Name}\t{e.JsonSha256}\t{e.CsvSha256}"));
}
