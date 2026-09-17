using System.Text;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>
/// The cut is exact wherever the top-level `run` property sits among its siblings: first, in the middle or last, the
/// result reads as the same document written without it (<see cref="RunPropertyCut"/>, above). Seen red once with the
/// span shifted by one byte (<c>document.AsSpan((int)cutEnd + 1)</c> in <c>RunPropertyCut.Cut</c>): all three cases
/// failed (`Assert.Equal() Failure: Strings differ`), each missing exactly the one byte immediately after the removed
/// span — the next property's opening quote when `run` is first, the separator comma when it is not — and the "run
/// last" case besides carried a trailing NUL from the now-oversized destination span; reverted immediately.
/// </summary>
public sealed class RunPropertyCutTests
{
    private const string RunFirst = "{\n  \"run\": {\n    \"a\": 1\n  },\n  \"before\": false,\n  \"after\": [\n    1,\n    2\n  ]\n}";
    private const string RunMiddle = "{\n  \"before\": false,\n  \"run\": {\n    \"a\": 1\n  },\n  \"after\": [\n    1,\n    2\n  ]\n}";
    private const string RunLast = "{\n  \"before\": false,\n  \"after\": [\n    1,\n    2\n  ],\n  \"run\": {\n    \"a\": 1\n  }\n}";
    private const string WithoutRun = "{\n  \"before\": false,\n  \"after\": [\n    1,\n    2\n  ]\n}";

    [Theory]
    [InlineData(RunFirst)]
    [InlineData(RunMiddle)]
    [InlineData(RunLast)]
    public void The_top_level_run_property_is_cut_wherever_it_appears(string withRun)
    {
        var cut = RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(withRun), "test document");
        Assert.Equal(WithoutRun, Encoding.UTF8.GetString(cut));
    }

    [Fact]
    public void A_missing_top_level_run_property_fails_instead_of_hashing()
    {
        var withoutRun = Encoding.UTF8.GetBytes(WithoutRun);
        var exception = Assert.Throws<InvalidOperationException>(() => RunPropertyCut.Bytes(withoutRun, "no run"));
        Assert.Contains("no top-level 'run' property", exception.Message);
    }

    [Fact]
    public void A_duplicated_top_level_run_property_fails_instead_of_hashing()
    {
        const string doubled = "{\n  \"run\": 1,\n  \"before\": false,\n  \"run\": 2\n}";
        var exception = Assert.Throws<InvalidOperationException>(() => RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(doubled), "doubled run"));
        Assert.Contains("more than one top-level 'run' property", exception.Message);
    }
}
