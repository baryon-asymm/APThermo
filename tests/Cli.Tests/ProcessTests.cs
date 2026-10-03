using APThermo.Execution;

namespace APThermo.Cli.Tests;

/// <summary>The command line as a separate process: real exit codes and standard streams, one run per exit code.</summary>
[Collection("cli")]
[Trait("Category", "EndToEnd")]
public sealed class ProcessTests
{
    /// <summary>The executable writes the document to standard output with exit 0.</summary>
    [Fact]
    public void TheExecutableWritesTheDocumentToStandardOutputWithExit0()
    {
        var run = CliFixture.Shared.InvokeProcess(CliFixture.Shared.Solving("rocket", CliFixture.Document("rocket-lox-lh2.json")));
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
        var run = CliFixture.Shared.InvokeProcess(CliFixture.Shared.Solving("rocket", CliFixture.Document("rocket-failing.json")));
        Assert.Equal(1, run.Code);
        using var document = run.Json();
        Assert.NotEqual("ok", document.RootElement.GetProperty("cases")[0].GetProperty("status").GetString());
    }

    /// <summary>The executable reports invalid input on standard error with exit 2.</summary>
    [Fact]
    public void TheExecutableReportsInvalidInputOnStandardErrorWithExit2()
    {
        var run = CliFixture.Shared.InvokeProcess(CliFixture.Shared.Solving("rocket", CliFixture.Document(Path.Combine("invalid", "unknown-field.json"))));
        Assert.Equal(2, run.Code);
        Assert.Contains("unknown field 'expansionRatio'", run.Error);
        Assert.Empty(run.Output);
    }

    /// <summary>The executable reports a forbidden accelerator with exit 3.</summary>
    [Fact]
    public void TheExecutableReportsAForbiddenAcceleratorWithExit3()
    {
        var run = CliFixture.Shared.InvokeProcess(["rocket", CliFixture.Document("rocket-lox-lh2.json"), "--database", CliFixture.Shared.DatabasePath, "--accelerator", "cuda"],
                                        new Dictionary<string, string> { [EngineOptions.NoCudaVariable] = "1" });
        Assert.Equal(3, run.Code);
        Assert.Contains(EngineOptions.NoCudaVariable, run.Error);
        Assert.Empty(run.Output);
    }

    /// <summary>The executable prints its version with exit 0.</summary>
    [Fact]
    public void TheExecutablePrintsItsVersionWithExit0()
    {
        var run = CliFixture.Shared.InvokeProcess(["--version"]);
        Assert.Equal(0, run.Code);
        Assert.Equal(Program.Version, run.Output.Trim());
        Assert.Empty(run.Error);
    }

    /// <summary>Working directory is <see cref="CliFixture.Temp"/>, an empty directory: no data/ beside it, no --database.</summary>
    [Fact]
    public void TheExecutableUsesTheEmbeddedDatabaseFromAnEmptyWorkingDirectory()
    {
        var run = CliFixture.Shared.InvokeProcess(["species"]);
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        using var document = run.Json();
        var database = document.RootElement.GetProperty("run").GetProperty("database");
        Assert.Equal(DatabaseFiles.EmbeddedThermoMarker, database.GetProperty("thermoPath").GetString());
        Assert.Equal(DatabaseFiles.EmbeddedTransMarker, database.GetProperty("transPath").GetString());
    }

    /// <summary>
    /// An empty <c>--output</c> value is exit 2 naming the option, as a real process (2026-09-26, the audit's
    /// finding 9): it used to reach <c>File.WriteAllText</c> with an empty path, an unhandled
    /// <see cref="ArgumentException"/> the process-level handler turned into exit 3.
    /// </summary>
    [Fact]
    public void AnEmptyOutputValueIsExit2AsARealProcess()
    {
        var run = CliFixture.Shared.InvokeProcess(CliFixture.Shared.Solving("rocket", CliFixture.Document("rocket-lox-lh2.json"), "--output="));
        Assert.Equal(2, run.Code);
        Assert.Contains("--output", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>
    /// An empty <c>--database</c> value is exit 2 naming the option even when the working directory itself holds a
    /// <c>thermo.inp</c> (2026-09-26, the audit's exact reproduction of finding 9): it used to combine to the bare
    /// file name and silently read whatever the working directory happened to hold, exit 0.
    /// </summary>
    [Fact]
    public void AnEmptyDatabaseValueIsExit2EvenWhenTheWorkingDirectoryHoldsAThermoInpFile()
    {
        File.Copy(Path.Combine(CliFixture.Shared.DatabasePath, "thermo.inp"), Path.Combine(CliFixture.Shared.Temp, "thermo.inp"), overwrite: true);
        try
        {
            var run = CliFixture.Shared.InvokeProcess(["species", "--database="]);
            Assert.Equal(2, run.Code);
            Assert.Contains("--database", run.Error, StringComparison.Ordinal);
            Assert.Empty(run.Output);
        }
        finally
        {
            File.Delete(Path.Combine(CliFixture.Shared.Temp, "thermo.inp"));
        }
    }
}
