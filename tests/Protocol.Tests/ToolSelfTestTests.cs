namespace APThermo.Protocol.Tests;

/// <summary>
/// Tool self-tests level: every Python tool node keeps a <c>test_*.py</c> that proves its own checks non-degenerate, and
/// nothing else runs it; here each is run as a process from the tree root. The list is found by the walk of <c>tools/</c>,
/// never typed. The cases of this class run one after another (one class is one xunit collection), so the temp directories
/// the self-tests build never meet.
/// </summary>
[Trait("Category", "EndToEnd")]
public sealed class ToolSelfTestTests
{
    private const int TailLines = 40;

    /// <summary>A hang guard, not a budget: the worst self-test (the merge guard's, 65 cases on 8 threads) took 7.7 s idle and
    /// 271 s with 64 CPU-bound processes on 16 cores; twice that, rounded up to ten minutes (this node's BOOT.md).</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(10);

    /// <summary>Every self-test script under <c>tools/</c>, as a path relative to the tree root with forward slashes, in path
    /// order: one case of <see cref="EverySelfTestExitsZero"/> each.</summary>
    public static TheoryData<string> SelfTests() => [.. Scripts()];

    /// <summary>The walk finds at least one self-test: a walk that finds none would make the Theory pass with no case.</summary>
    [Fact]
    public void TheWalkFindsTheSelfTests() => Assert.NotEmpty(Scripts());

    /// <summary>One tool node's self-test, run as <c>python -X utf8 &lt;script&gt;</c> from the tree root, exits with zero.</summary>
    /// <param name="script">The path of the self-test relative to the tree root.</param>
    [Theory]
    [MemberData(nameof(SelfTests))]
    public async Task EverySelfTestExitsZero(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var run = await PythonProcess.RunAsync(script, [], Deadline).ConfigureAwait(true);

        var tail = string.Join('\n', run.Output.Split('\n').TakeLast(TailLines));
        Assert.True(run.ExitCode == 0, $"{script} exited with {run.ExitCode}; the last {TailLines} lines of its output:\n{tail}");
    }

    private static List<string> Scripts()
    {
        var tools = Path.Combine(Tree.Root, "tools");
        return !Directory.Exists(tools)
            ? []
            : [.. Directory.EnumerateDirectories(tools)
                .Where(directory => !Tree.Skipped.Contains(Path.GetFileName(directory)))
                .SelectMany(directory => Directory.EnumerateFiles(directory, "test_*.py", SearchOption.TopDirectoryOnly))
                .Select(Tree.Relative)
                .Order(StringComparer.Ordinal)];
    }
}
