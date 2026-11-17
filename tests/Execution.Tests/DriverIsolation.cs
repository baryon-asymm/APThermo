using System.Text;
using System.Xml.Linq;
using APThermo.Harness;

namespace APThermo.Execution.Tests;

/// <summary>Runs the deliberate CUDA-driver probes in an owned, bounded VSTest process.</summary>
internal static class DriverIsolation
{
    private const string Sentinel = "APTHERMO_ISOLATED_CUDA_PROBE";

    internal static bool IsChild(string mode, out string marker, out string nonce)
    {
        var value = Environment.GetEnvironmentVariable(Sentinel);
        marker = string.Empty;
        nonce = string.Empty;
        if (value is null) { return false; }
        (marker, nonce) = Parse(value, mode);
        return true;
    }

    internal static (string Marker, string Nonce) Parse(string value, string mode)
    {
        var parts = value.Split('|');
        var valid = parts.Length == 3 && parts[0] == mode && Guid.TryParseExact(parts[1], "N", out _) &&
            Path.IsPathFullyQualified(parts[2]) && Path.GetFileName(parts[2]) == "completed-" + parts[1] &&
            string.Equals(Path.GetFullPath(Path.GetDirectoryName(parts[2])!),
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), "apthermo-cuda-probe-" + parts[1])),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return valid ? (parts[2], parts[1]) : throw new InvalidOperationException("Invalid isolated CUDA probe sentinel");
    }

    internal static void Complete(string marker, string nonce)
    {
        using var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(nonce);
    }

    internal static void Run(string method, string mode)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "apthermo-cuda-probe-" + nonce);
        _ = Directory.CreateDirectory(directory);
        Exception? primary = null;
        try
        {
            var marker = Path.Combine(directory, "completed-" + nonce);
            var name = typeof(AcceleratorChoiceTests).FullName + "." + method;
            var environment = new Dictionary<string, string> { [Sentinel] = mode + "|" + nonce + "|" + marker };
            var arguments = new[] { typeof(AcceleratorChoiceTests).Assembly.Location, "--filter", "FullyQualifiedName=" + name,
                "--logger", "trx;LogFileName=probe.trx", "--results-directory", directory };
            var result = DotnetProcess.Run("test", arguments, directory, environment);
            Assert.True(result.ExitCode == 0, Encoding.UTF8.GetString(result.Output.Span) + Encoding.UTF8.GetString(result.Error.Span));
            VerifyReport(Path.Combine(directory, "probe.trx"), name);
            Assert.True(File.Exists(marker), "isolated CUDA scenario did not reach completion");
            Assert.Equal(nonce, File.ReadAllText(marker));
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            if (primary is null) { Directory.Delete(directory, recursive: true); }
            else
            {
                try { Directory.Delete(directory, recursive: true); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    primary.Data["DirectoryCleanupException"] = cleanup;
                }
            }
        }
    }

    private static void VerifyReport(string path, string expected)
    {
        var document = XDocument.Load(path);
        var rows = document.Descendants().Where(element => element.Name.LocalName == "UnitTestResult").ToArray();
        _ = Assert.Single(rows);
        Assert.Equal(expected, (string?)rows[0].Attribute("testName"));
        Assert.Equal("Passed", (string?)rows[0].Attribute("outcome"));
        var counters = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Counters");
        Assert.Equal("1", (string?)counters.Attribute("total"));
        Assert.Equal("1", (string?)counters.Attribute("executed"));
        Assert.Equal("1", (string?)counters.Attribute("passed"));
        Assert.Equal("0", (string?)counters.Attribute("failed"));
        Assert.Equal("0", (string?)counters.Attribute("notExecuted"));
    }
}
