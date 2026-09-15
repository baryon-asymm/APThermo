using APThermo.Execution;

namespace APThermo.Cli.Tests;

/// <summary>The command line as a separate process: real exit codes and standard streams, one run per exit code.</summary>
[Collection(CliCollection.Name)]
public sealed class ProcessTests(CliFixture fixture)
{
    [Fact]
    public void The_executable_writes_the_document_to_standard_output_with_exit_0()
    {
        var run = fixture.InvokeProcess(fixture.Solving("rocket", fixture.Document("rocket-lox-lh2.json")));
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        Assert.Empty(run.Error);
        using var document = run.Json();
        Assert.Equal("apthermo", document.RootElement.GetProperty("run").GetProperty("tool").GetString());
        Assert.Equal(Program.Version, document.RootElement.GetProperty("run").GetProperty("version").GetString());
        Assert.Equal("ok", document.RootElement.GetProperty("cases")[0].GetProperty("status").GetString());
    }

    [Fact]
    public void The_executable_returns_1_for_a_failing_case_and_still_writes_the_document()
    {
        var run = fixture.InvokeProcess(fixture.Solving("rocket", fixture.Document("rocket-failing.json")));
        Assert.Equal(1, run.Code);
        using var document = run.Json();
        Assert.NotEqual("ok", document.RootElement.GetProperty("cases")[0].GetProperty("status").GetString());
    }

    [Fact]
    public void The_executable_reports_invalid_input_on_standard_error_with_exit_2()
    {
        var run = fixture.InvokeProcess(fixture.Solving("rocket", fixture.Document(Path.Combine("invalid", "unknown-field.json"))));
        Assert.Equal(2, run.Code);
        Assert.Contains("unknown field 'expansionRatio'", run.Error);
        Assert.Empty(run.Output);
    }

    [Fact]
    public void The_executable_reports_a_forbidden_accelerator_with_exit_3()
    {
        var run = fixture.InvokeProcess(["rocket", fixture.Document("rocket-lox-lh2.json"), "--database", fixture.DatabasePath, "--accelerator", "cuda"],
                                        new Dictionary<string, string> { [EngineOptions.NoCudaVariable] = "1" });
        Assert.Equal(3, run.Code);
        Assert.Contains(EngineOptions.NoCudaVariable, run.Error);
        Assert.Empty(run.Output);
    }
}
