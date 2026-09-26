using System.Text;
using APThermo.Fixtures;

namespace APThermo.Docs.Tests;

/// <summary>
/// L2 (BOOT.md): every scenario of the samples node, run in-process through its tree contract (<c>Program.Run</c>),
/// prints its approved output, whole file. On a mismatch the actual bytes are written as `&lt;name&gt;.actual.txt`
/// beside the approved one (git-ignored) and the test fails naming both paths. The CPU accelerator is pinned in the
/// scenarios themselves, so the approved bytes hold on every machine.
/// </summary>
public sealed class SampleOutputTests
{
    /// <summary>The theory data of (scenario, class name) pairs, one per sample scenario.</summary>
    public static TheoryData<string, string> Scenarios()
    {
        var data = new TheoryData<string, string>();
        foreach (var name in Samples.Program.Scenarios)
        {
            data.Add(name, Samples.Program.ClassNameOf(name));
        }

        return data;
    }

    /// <summary>The scenario prints its approved output.</summary>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public void TheScenarioPrintsItsApprovedOutput(string scenario, string className)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Samples.Program.Run([scenario], output, error);
        Assert.True(code == 0, $"the scenario '{scenario}' exited with {code}: {error}");
        Assert.True(error.ToString().Length == 0, $"the scenario '{scenario}' wrote to standard error: {error}");

        var approvedPath = RepositoryPaths.Resolve("tests", "Docs.Tests", "approved", "samples", className + ".approved.txt");
        var actual = GuideDocuments.Lf(output.ToString());
        if (!File.Exists(approvedPath))
        {
            Assert.Fail($"the approved output of '{scenario}' is missing: {approvedPath}");
        }

        var approved = GuideDocuments.Lf(File.ReadAllText(approvedPath));
        if (!approved.Equals(actual, StringComparison.Ordinal))
        {
            var actualPath = Path.Combine(Path.GetDirectoryName(approvedPath)!, className + ".actual.txt");
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.Fail($"the output of '{scenario}' differs from its approved file: approved {approvedPath}, actual {actualPath}");
        }
    }
}
