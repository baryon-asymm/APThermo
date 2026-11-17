using System.Text;
using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>The real entry point overrides a controlled initial process writer encoding.</summary>
[Collection(CliFixture.CollectionName)]
[Trait("Category", "EndToEnd")]
public sealed class Utf8OutputProcessTests
{
    /// <summary>An owned process starts with CP866 and returns Unicode paths as UTF-8 without a BOM.</summary>
    [Fact]
    public void RedirectedOutputIsUtf8WithoutBomForUnicodeDatabasePath()
    {
        var directory = Directory.CreateDirectory(CliFixture.Shared.TempFile("test-\u042F\u0436")).FullName;
        File.Copy(Path.Combine(CliFixture.Shared.DatabasePath, "thermo.inp"), Path.Combine(directory, "thermo.inp"), true);
        File.Copy(Path.Combine(CliFixture.Shared.DatabasePath, "trans.inp"), Path.Combine(directory, "trans.inp"), true);
        var probe = DotnetProcess.BuiltAssemblyPath("APThermo.Cli.Tests.OemProbe", "tests", "Cli.Tests", "OemProbe");
        var result = DotnetProcess.Run(probe, ["species", "--database", directory], CliFixture.Shared.Temp);
        var error = Encoding.UTF8.GetString(result.Error.Span);
        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {error}");
        Assert.Contains("initialWriterCodePage=866", error, StringComparison.Ordinal);
        Assert.False(result.Output.Span.StartsWith(Encoding.UTF8.Preamble));
        var text = new UTF8Encoding(false, true).GetString(result.Output.Span);
        using var json = JsonDocument.Parse(text);
        var actual = json.RootElement.GetProperty("run").GetProperty("database").GetProperty("thermoPath").GetString();
        Assert.Equal(Path.Combine(directory, "thermo.inp"), actual);
    }

    /// <summary>Entry-point exit codes and flushed bytes survive the controlled writer.</summary>
    [Theory]
    [InlineData("--version", 0)]
    [InlineData("--not-a-real-option", 2)]
    public void OwnedProbePreservesExitAndFlush(string argument, int expected)
    {
        var probe = DotnetProcess.BuiltAssemblyPath("APThermo.Cli.Tests.OemProbe", "tests", "Cli.Tests", "OemProbe");
        var result = DotnetProcess.Run(probe, [argument], CliFixture.Shared.Temp);
        Assert.Equal(expected, result.ExitCode);
        Assert.Contains("initialWriterCodePage=866", Encoding.UTF8.GetString(result.Error.Span), StringComparison.Ordinal);
        if (expected == 0)
        {
            Assert.Equal(Program.Version, new UTF8Encoding(false, true).GetString(result.Output.Span).Trim());
        }
        else
        {
            Assert.True(result.Output.IsEmpty);
        }
    }

    /// <summary>Cancellation before launch retains the caller's token through the owned runner.</summary>
    [Fact]
    public void CancelledProbeRetainsCallerToken()
    {
        var probe = DotnetProcess.BuiltAssemblyPath("APThermo.Cli.Tests.OemProbe", "tests", "Cli.Tests", "OemProbe");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => DotnetProcess.Run(probe, ["--version"],
            CliFixture.Shared.Temp, DotnetProcess.DefaultTimeout, cancellationToken: cancelled.Token));
        Assert.Equal(cancelled.Token, error.CancellationToken);
    }
}
