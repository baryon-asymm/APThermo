using System.Diagnostics;
using System.Globalization;
using APThermo.Fixtures;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>
/// A fact that runs only when the environment variable <see cref="FullWorstCaseListsTests.Variable"/> names a directory holding
/// the full CORE-MATH worst-case lists (<c>exp.wc</c>, <c>log.wc</c>, <c>pow.wc</c>): the lists are not committed (the fixtures
/// hold a selection, <c>fixtures/PROVENANCE.txt</c>), so without them the fact is skipped with the reason below, and nothing else is.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class WorstCaseListsFactAttribute : FactAttribute
{
    /// <summary>Skips the fact, with the reason, when the variable is not set.</summary>
    public WorstCaseListsFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(FullWorstCaseListsTests.Variable)))
        {
            Skip = $"optional: set {FullWorstCaseListsTests.Variable} to a directory holding CORE-MATH's exp.wc, log.wc and pow.wc (tests/Thermo.Tests/Elementary/fixtures/PROVENANCE.txt)";
        }
    }
}

/// <summary>
/// The full published worst-case lists of CORE-MATH, about 2.3 million inputs, through the tree's own exp, log and pow, each
/// result compared bit for bit with the oracle (mpmath, 400 bits). Three steps: a script parses the lists into bit patterns,
/// the tree evaluates them, the script verifies the results (<c>check_worst_cases.py</c>).
/// </summary>
public sealed class FullWorstCaseListsTests
{
    /// <summary>The environment variable naming the directory of the full lists.</summary>
    public const string Variable = "APTHERMO_COREMATH_WC";

    /// <summary>Every input of the three full lists is correctly rounded.</summary>
    /// <returns>The task of the run.</returns>
    [WorstCaseListsFact]
    [Trait("Category", "LongRunning")]
    [Trait("Category", "EndToEnd")]
    public async Task TheFullWorstCaseListsAreCorrectlyRounded()
    {
        var lists = Environment.GetEnvironmentVariable(Variable)!;
        var work = Path.Combine(Path.GetTempPath(), "apthermo-worst-cases-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        _ = Directory.CreateDirectory(work);
        try
        {
            var (parsedExit, parsedOutput) = await Run(["inputs", lists, work]).ConfigureAwait(true);
            Assert.True(parsedExit == 0, parsedOutput);
            foreach (var function in ElementaryFixtures.Functions)
            {
                var inputs = await File.ReadAllLinesAsync(Path.Combine(work, function + ".in")).ConfigureAwait(true);
                var results = inputs.Where(line => line.Length > 0).Select(line => Evaluate(function, line)).ToList();
                Assert.NotEmpty(results);
                await File.WriteAllLinesAsync(Path.Combine(work, function + ".out"), results).ConfigureAwait(true);
            }

            var (verifiedExit, verifiedOutput) = await Run(["verify", lists, work]).ConfigureAwait(true);
            Assert.True(verifiedExit == 0, verifiedOutput);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    private static string Evaluate(string function, string line) =>
        ElementaryFixtures.Encode(ElementaryFixtures.Evaluate(function, [.. line.Split(' ').Select(Decode)]));

    private static double Decode(string token) =>
        BitConverter.Int64BitsToDouble(long.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static async Task<(int ExitCode, string Output)> Run(IReadOnlyList<string> arguments)
    {
        var script = Path.Combine(RepositoryPaths.Resolve("tests", "Thermo.Tests", "Elementary"), "check_worst_cases.py");
        var start = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in (string[])["-X", "utf8", script, .. arguments])
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(true);
        return (process.ExitCode, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
    }
}
