using System.Text;
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
        var examples = BitExamples.ComputeAll(fixture);
        var problems = new List<string>();
        foreach (var example in examples)
        {
            var problem = snapshot.Problem(example.Name, $"{example.JsonSha256}\t{example.CsvSha256}");
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        problems.AddRange(snapshot.StaleKeys(examples.Select(e => e.Name))
            .Select(key => $"{key}: recorded in {ApprovedPath}, but no example produces it; delete the line in the commit that removed the example"));

        Assert.True(problems.Count == 0, $"{problems.Count} example(s) no longer give the recorded output:\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// The text this fixture captures in process is exactly what `--output` writes to a file: no byte-order mark, no
    /// newline translation on the way to either destination. `DocumentWriter.Render` builds one string; `Deliver`
    /// either hands it to the `TextWriter` (what <see cref="CliFixture.Invoke"/> captures) or to
    /// `File.WriteAllText` with a no-BOM UTF-8 encoding — never both from the same run. Proven here for the LOX/LH2
    /// rocket example by running it twice, once captured in process and once delivered to a file, and comparing what
    /// is left of each after the same `run`-cutting the Bits level hashes through: `run.timings` differs run to run,
    /// nothing else does, since the CPU accelerator is deterministic on the same document.
    /// </summary>
    [Fact]
    public void The_captured_text_matches_the_bytes_delivered_to_the_output_file()
    {
        var document = fixture.Document("rocket-lox-lh2.json");
        var captured = fixture.Invoke(fixture.Solving("rocket", document));
        Assert.True(captured.Code is 0 or 1, $"exit code {captured.Code}: {captured.Error}");

        var path = fixture.TempFile("delivered-lox-lh2.json");
        var delivered = fixture.Invoke(fixture.Solving("rocket", document, "--output", path));
        Assert.True(delivered.Code is 0 or 1, $"exit code {delivered.Code}: {delivered.Error}");

        var capturedBytes = RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(captured.Output), "the captured text");
        var deliveredBytes = RunPropertyCut.Bytes(File.ReadAllBytes(path), "the delivered file");
        Assert.Equal(capturedBytes, deliveredBytes);
    }
}

/// <summary>
/// One example's recorded output: the SHA-256 of the bytes the command line delivers for its JSON document with the
/// top-level `run` property cut out (<see cref="RunPropertyCut"/>), and the SHA-256 of its CSV text as written.
/// </summary>
internal sealed record BitExample(string Name, string JsonSha256, string CsvSha256);

/// <summary>One example's JSON and CSV text, exactly as the command line produced them, before either is hashed.</summary>
internal sealed record RawExample(string Name, string Json, string Csv);

/// <summary>
/// Every example the Bits level covers (BOOT.md, the Bits row): the problem and states documents of documents/, the
/// problem and record examples of the Cli API read from API.md, and the species listing. Gathered, never typed, so the
/// list cannot fall behind documents/ or API.md.
/// </summary>
internal static class BitExamples
{
    public static IReadOnlyList<BitExample> ComputeAll(CliFixture fixture) =>
        RawExamples(fixture).Select(e => new BitExample(e.Name, JsonSha256(e.Json, e.Name), Sha256(e.Csv))).ToList();

    /// <summary>The same examples, before either the JSON or the CSV text is hashed (the delivered-bytes proof above reads this).</summary>
    public static IReadOnlyList<RawExample> RawExamples(CliFixture fixture)
    {
        var examples = new List<RawExample>();
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
    private static IEnumerable<RawExample> ApiProblemExamples(CliFixture fixture)
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
    private static IEnumerable<RawExample> ApiRecordExamples(CliFixture fixture)
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

    private static RawExample FromArgs(CliFixture fixture, string name, string[] jsonArgs, string[] csvArgs)
    {
        var json = fixture.Invoke(jsonArgs);
        Assert.True(json.Code is 0 or 1, $"{name}: exit code {json.Code}: {json.Error}");
        var csv = fixture.Invoke(csvArgs);
        Assert.True(csv.Code is 0 or 1, $"{name}: exit code {csv.Code} (csv): {csv.Error}");
        return new RawExample(name, json.Output, csv.Output);
    }

    /// <summary>
    /// The SHA-256 of the bytes the command line delivers for a JSON document — proven equal to the bytes `--output`
    /// writes to a file, <see cref="BitSnapshotTests.The_captured_text_matches_the_bytes_delivered_to_the_output_file"/> —
    /// with the top-level `run` property cut out: its name, its value and one adjacent separator with the surrounding
    /// white space, found by walking the document's top-level properties with a `Utf8JsonReader`, never by searching
    /// the text for `"run"` (<see cref="RunPropertyCut"/>, proven with `run` first, in the middle and last,
    /// <see cref="RunPropertyCutTests"/>). Indentation, line breaks, the final newline, string escaping and the key
    /// order of every other property stay in. A document with no top-level `run` property, or with more than one,
    /// fails the test that calls this method; it never produces a hash (BOOT.md, the Bits level).
    /// </summary>
    public static string JsonSha256(string json, string example) =>
        Sha256(Encoding.UTF8.GetString(RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(json), example)));

    private static string Sha256(string text) => new BitHash().Add(text).ToHex();
}
