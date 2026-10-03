namespace APThermo.Protocol.Tests;

/// <summary>Lint level: the file half of the protocol, run as the linter process the loader names, in strict mode (the tree's criterion is zero warnings).</summary>
[Trait("Category", "EndToEnd")]
public sealed class LintTests
{
    /// <summary>The tree passes the protocol linter with no error and no warning (strict mode: a warning fails too).</summary>
    [Fact]
    public async Task TheTreePassesTheProtocolLinterWithNoErrorAndNoWarning()
    {
        var script = Path.Combine("tools", "protocol-lint", "protocol_lint.py");
        var run = await PythonProcess.RunAsync(script, [".", "--exclude", "templates", "--strict"], TimeSpan.FromMinutes(2)).ConfigureAwait(true);

        Assert.True(run.ExitCode == 0, $"protocol_lint exited with {run.ExitCode} (strict: a warning fails too):\n{run.Output}");
    }
}
