using System.Security.Cryptography;
using System.Text.Json;
using APThermo.Fixtures;
using Xunit.Abstractions;

namespace APThermo.Docs.Tests;

/// <summary>Pure injected controls for cold invocation diagnostics; never launch a CLI.</summary>
/// <param name="output">Captures injected diagnostic records in the real TRX output channel.</param>
public sealed class ColdCliDiagnosticTests(ITestOutputHelper output)
{
    private static readonly ColdCliSettings Settings = new("Field", Path.Combine(RepositoryPaths.Root, "APThermo.Cli.dll"), Path.GetTempPath());

    /// <summary>Context is logged before the supplied runner and original normal results are returned.</summary>
    [Fact]
    public void NormalRunnerSeesStartContextAndKeepsResultIdentity()
    {
        var lines = new List<string>();
        var (where, args, input) = ActualExample();
        var expected = new CliRun(17, "original output", "original error");
        var diagnostic = new ColdCliInvocation(Settings, passed =>
        {
            Assert.Same(args, passed);
            _ = Assert.Single(lines);
            CheckContext(lines[0], where, input, 1, args);
            return expected;
        }, record => Capture(lines, record));
        Assert.Same(expected, diagnostic.Run(where, args));
        Assert.Equal(2, lines.Count);
        using var finish = JsonDocument.Parse(lines[1]);
        Assert.Equal("finish", finish.RootElement.GetProperty("Phase").GetString());
        Assert.Equal(17, finish.RootElement.GetProperty("ExitCode").GetInt32());
        Assert.True(finish.RootElement.GetProperty("ElapsedSeconds").GetDouble() >= 0);
    }

    /// <summary>An injected timeout keeps its object, stack and original raw arrays while gaining invocation context.</summary>
    [Fact]
    public void TimeoutRunnerKeepsExceptionAndRawArrayIdentity()
    {
        var lines = new List<string>();
        var (where, args, input) = ActualExample();
        var original = new TimeoutException("injected timeout");
        byte[] output = [0, 255, 10];
        byte[] error = [42, 0];
        original.Data["StandardOutput"] = output;
        original.Data["StandardError"] = error;
        string? originalStack = null;
        var diagnostic = new ColdCliInvocation(Settings, _ =>
        {
            try
            {
                return ThrowOriginal(original);
            }
            catch (TimeoutException problem)
            {
                originalStack = problem.StackTrace;
                throw;
            }
        }, record => Capture(lines, record));
        var actual = Assert.Throws<TimeoutException>(() => diagnostic.Run(where, args));
        Assert.Same(original, actual);
        Assert.Same(output, actual.Data["StandardOutput"]);
        Assert.Same(error, actual.Data["StandardError"]);
        CheckContext(Assert.IsType<string>(actual.Data[ColdCliInvocation.ContextKey]), where, input, 1, args);
        Assert.Contains(nameof(ThrowOriginal), actual.StackTrace, StringComparison.Ordinal);
        Assert.StartsWith(originalStack!, actual.StackTrace, StringComparison.Ordinal);
        Assert.Equal(2, lines.Count);
    }

    /// <summary>A normal return is never replaced by a wrapper.</summary>
    [Fact]
    public void SuppliedNormalResultIsReturnedUnchanged()
    {
        var expected = new CliRun(0, "output", "error");
        var diagnostic = new ColdCliInvocation(Settings, _ => expected, _ => { });
        Assert.Same(expected, diagnostic.Run("synthetic.md:1", ["--help"]));
    }

    /// <summary>Failing diagnostic output cannot replace either a supplied result or a supplied failure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticSinkFailureCannotReplaceRunnerOutcome(bool fails)
    {
        var expected = new CliRun(0, "unchanged", "unchanged");
        var original = new TimeoutException("original process failure");
        var attempts = 0;
        var calls = 0;
        var diagnostic = new ColdCliInvocation(Settings, _ =>
        {
            calls++;
            return fails ? ThrowOriginal(original) : expected;
        }, _ =>
        {
            attempts++;
            throw new InvalidOperationException("injected diagnostic sink failure");
        });
        if (fails)
        {
            var actual = Assert.Throws<TimeoutException>(() => diagnostic.Run("synthetic.md:7", ["--help"]));
            Assert.Same(original, actual);
            _ = Assert.IsType<string>(actual.Data[ColdCliInvocation.ContextKey]);
            _ = Assert.IsType<string>(actual.Data[ColdCliInvocation.OutcomeKey]);
        }
        else
        {
            Assert.Same(expected, diagnostic.Run("synthetic.md:7", ["--help"]));
        }
        Assert.Equal(1, calls);
        Assert.Equal(2, attempts);
    }

    /// <summary>Caller cancellation remains the same exception and token.</summary>
    [Fact]
    public void CancellationIdentityAndOutcomeRemainOriginal()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var original = new OperationCanceledException(cancellation.Token);
        var lines = new List<string>();
        var diagnostic = new ColdCliInvocation(Settings, _ => ThrowOriginal(original), record => Capture(lines, record));
        var actual = Assert.Throws<OperationCanceledException>(() => diagnostic.Run("synthetic.md:8", ["--help"]));
        Assert.Same(original, actual);
        Assert.Equal(cancellation.Token, actual.CancellationToken);
        Assert.Equal(2, lines.Count);
        using var failure = JsonDocument.Parse(lines[1]);
        Assert.Equal(typeof(OperationCanceledException).FullName, failure.RootElement.GetProperty("ExceptionType").GetString());
    }

    /// <summary>Duplicate source locations still execute separately and receive generated consecutive ordinals.</summary>
    [Theory]
    [InlineData("Field")]
    [InlineData("Exact")]
    public void DuplicateInvocationsKeepSeparateOrdinalsAndModes(string mode)
    {
        var (where, args, input) = ActualExample();
        var lines = new List<string>();
        var calls = 0;
        var diagnostic = new ColdCliInvocation(Settings with { Mode = mode }, _ =>
        {
            calls++;
            return new CliRun(0, "output", "error");
        }, record => Capture(lines, record));
        _ = diagnostic.Run(where, args);
        _ = diagnostic.Run(where, args);
        Assert.Equal(2, calls);
        Assert.Equal(4, lines.Count);
        CheckContext(lines[0], where, input, 1, args, mode);
        CheckContext(lines[2], where, input, 2, args, mode);
    }

    /// <summary>In-process facts can be constructed without any process diagnostic output.</summary>
    [Fact]
    public void InProcessFactsRequireNoProcessLogger() =>
        _ = Assert.IsType<CommandLineExampleTests>(new CommandLineExampleTests());

    private void Capture(List<string> lines, string record)
    {
        lines.Add(record);
        output.WriteLine(record);
    }

    private static CliRun ThrowOriginal(Exception problem) => throw problem;

    private static void CheckContext(string record, string where, string input, int ordinal, IReadOnlyList<string> expectedArgs, string? mode = null)
    {
        using var document = JsonDocument.Parse(record);
        var context = document.RootElement.GetProperty("Context");
        Assert.Equal(ordinal, context.GetProperty("Ordinal").GetInt32());
        Assert.Equal(where, context.GetProperty("Source").GetString());
        Assert.Equal(mode ?? Settings.Mode, context.GetProperty("Mode").GetString());
        var process = context.GetProperty("Process");
        Assert.Equal(expectedArgs, process.GetProperty("Arguments").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(Settings.AssemblyPath, process.GetProperty("AssemblyPath").GetString());
        Assert.Equal(Settings.WorkingDirectory, process.GetProperty("WorkingDirectory").GetString());
        Assert.Equal(TimeSpan.FromMinutes(5).TotalSeconds, process.GetProperty("TimeoutSeconds").GetDouble());
        Assert.Equal(TimeSpan.FromSeconds(5).TotalSeconds, process.GetProperty("CleanupSeconds").GetDouble());
        var observedInput = Assert.Single(context.GetProperty("Inputs").EnumerateArray());
        Assert.Equal(input, observedInput.GetProperty("Path").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))), observedInput.GetProperty("Sha256").GetString());
    }

    private static (string Where, string[] Args, string Input) ActualExample()
    {
        var page = RepositoryPaths.Resolve("README.md");
        var lines = File.ReadAllLines(page);
        var line = Array.FindIndex(lines, text => text.Trim().TrimStart('$').TrimStart().StartsWith("apthermo rocket ", StringComparison.Ordinal));
        Assert.True(line >= 0, "the committed README has no rocket invocation");
        var args = lines[line].Trim().TrimStart('$').TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..];
        var input = args.Single(token => token.StartsWith("samples/cli/", StringComparison.Ordinal));
        var full = Path.GetFullPath(Path.Combine(RepositoryPaths.Root, input));
        return ($"{page}:{line + 1}", args.Select(token => token == input ? full : token).ToArray(), full);
    }
}
