using APThermo.Execution;

namespace APThermo.Cli.Tests;

/// <summary>The command line as a separate process: real exit codes and standard streams, one run per exit code.</summary>
[Collection(CliCollectionDefinition.Name)]
public sealed class ProcessTests(CliFixture fixture)
{
    /// <summary>The executable writes the document to standard output with exit 0.</summary>
    [Fact]
    public void TheExecutableWritesTheDocumentToStandardOutputWithExit0()
    {
        var run = fixture.InvokeProcess(fixture.Solving("rocket", CliFixture.Document("rocket-lox-lh2.json")));
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        Assert.Empty(run.Error);
        using var document = run.Json();
        Assert.Equal("apthermo", document.RootElement.GetProperty("run").GetProperty("tool").GetString());
        Assert.Equal(Program.Version, document.RootElement.GetProperty("run").GetProperty("version").GetString());
        Assert.Equal("ok", document.RootElement.GetProperty("cases")[0].GetProperty("status").GetString());
    }

    /// <summary>The executable returns 1 for a failing case and still writes the document.</summary>
    [Fact]
    public void TheExecutableReturns1ForAFailingCaseAndStillWritesTheDocument()
    {
        var run = fixture.InvokeProcess(fixture.Solving("rocket", CliFixture.Document("rocket-failing.json")));
        Assert.Equal(1, run.Code);
        using var document = run.Json();
        Assert.NotEqual("ok", document.RootElement.GetProperty("cases")[0].GetProperty("status").GetString());
    }

    /// <summary>The executable reports invalid input on standard error with exit 2.</summary>
    [Fact]
    public void TheExecutableReportsInvalidInputOnStandardErrorWithExit2()
    {
        var run = fixture.InvokeProcess(fixture.Solving("rocket", CliFixture.Document(Path.Combine("invalid", "unknown-field.json"))));
        Assert.Equal(2, run.Code);
        Assert.Contains("unknown field 'expansionRatio'", run.Error);
        Assert.Empty(run.Output);
    }

    /// <summary>The executable reports a forbidden accelerator with exit 3.</summary>
    [Fact]
    public void TheExecutableReportsAForbiddenAcceleratorWithExit3()
    {
        var run = fixture.InvokeProcess(["rocket", CliFixture.Document("rocket-lox-lh2.json"), "--database", fixture.DatabasePath, "--accelerator", "cuda"],
                                        new Dictionary<string, string> { [EngineOptions.NoCudaVariable] = "1" });
        Assert.Equal(3, run.Code);
        Assert.Contains(EngineOptions.NoCudaVariable, run.Error);
        Assert.Empty(run.Output);
    }

    /// <summary>The executable prints its version with exit 0.</summary>
    [Fact]
    public void TheExecutablePrintsItsVersionWithExit0()
    {
        var run = fixture.InvokeProcess(["--version"]);
        Assert.Equal(0, run.Code);
        Assert.Equal(Program.Version, run.Output.Trim());
        Assert.Empty(run.Error);
    }

    /// <summary>Working directory is <see cref="CliFixture.Temp"/>, an empty directory: no data/ beside it, no --database.</summary>
    [Fact]
    public void TheExecutableUsesTheEmbeddedDatabaseFromAnEmptyWorkingDirectory()
    {
        var run = fixture.InvokeProcess(["species"]);
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        using var document = run.Json();
        var database = document.RootElement.GetProperty("run").GetProperty("database");
        Assert.Equal(DatabaseFiles.EmbeddedThermoMarker, database.GetProperty("thermoPath").GetString());
        Assert.Equal(DatabaseFiles.EmbeddedTransMarker, database.GetProperty("transPath").GetString());
    }
}
