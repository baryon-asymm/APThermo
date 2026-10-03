using System.Text;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>
/// The Bits level's examples run as fresh processes, the shipped cold path (BOOT.md, the Bits row): every example of
/// <see cref="BitExamples.Specs"/>, JSON and CSV, as <c>dotnet APThermo.Cli.dll</c> with the raw bytes of standard
/// output hashed as the in-process level hashes its text. Four examples run at a time: each process pays its own
/// start-up and kernel compile, and the machine's cores are the limit.
/// </summary>
internal static class ProcessBitExamples
{
    private const int Parallelism = 4;

    /// <summary>Every example as a process, hashed, in the order of <see cref="BitExamples.Specs"/>.</summary>
    public static IReadOnlyList<BitExample> ComputeAll()
    {
        var specs = BitExamples.Specs();
        Assert.NotEmpty(specs);
        var assembly = CliFixture.CliAssemblyPath();
        var examples = new BitExample[specs.Count];
        _ = Parallel.For(0, specs.Count, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, index =>
        {
            var spec = specs[index];
            var json = Run(assembly, spec.Name, spec.JsonArgs, "");
            var csv = Run(assembly, spec.Name, spec.CsvArgs, " (csv)");
            examples[index] = new BitExample(spec.Name, BitExamples.JsonSha256(json.ToArray(), spec.Name), BitExamples.Sha256(Encoding.UTF8.GetString(csv.Span)));
        });
        return examples;
    }

    private static ReadOnlyMemory<byte> Run(string assembly, string name, IReadOnlyList<string> args, string which)
    {
        var result = DotnetProcess.Run(assembly, args, CliFixture.Shared.Temp);
        Assert.True(result.ExitCode is 0 or 1, $"{name}: exit code {result.ExitCode}{which}: {Encoding.UTF8.GetString(result.Error.Span)}");
        return result.Output;
    }
}
