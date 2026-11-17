using System.Diagnostics;
using System.Text;
using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>Redirected stdout uses UTF-8 in a real process, independent of the Windows console code page.</summary>
[Collection(CliFixture.CollectionName)]
[Trait("Category", "EndToEnd")]
public sealed class Utf8OutputProcessTests
{
    /// <summary>Windows uses a private hidden console set to OEM 866; other hosts use their real process path.</summary>
    [Fact]
    public void RedirectedOutputIsUtf8WithoutBomForUnicodeDatabasePath()
    {
        var directory = Directory.CreateDirectory(CliFixture.Shared.TempFile("база данных")).FullName;
        File.Copy(Path.Combine(CliFixture.Shared.DatabasePath, "thermo.inp"), Path.Combine(directory, "thermo.inp"), true);
        File.Copy(Path.Combine(CliFixture.Shared.DatabasePath, "trans.inp"), Path.Combine(directory, "trans.inp"), true);
        var bytes = OperatingSystem.IsWindows() ? RunInOemConsole(directory) : RunDirectly(directory);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        var text = new UTF8Encoding(false, true).GetString(bytes);
        using var json = JsonDocument.Parse(text);
        var actual = json.RootElement.GetProperty("run").GetProperty("database").GetProperty("thermoPath").GetString();
        Assert.Equal(Path.Combine(directory, "thermo.inp"), actual);
    }

    private static byte[] RunDirectly(string directory)
    {
        var result = DotnetProcess.Run(CliFixture.CliAssemblyPath(), ["species", "--database", directory], CliFixture.Shared.Temp);
        Assert.True(result.ExitCode == 0, Encoding.UTF8.GetString(result.Error.Span));
        return result.Output.ToArray();
    }

    private static byte[] RunInOemConsole(string directory)
    {
        var page = CliFixture.Shared.TempFile("oem-page.txt");
        var output = CliFixture.Shared.TempFile("oem-output.json");
        var error = CliFixture.Shared.TempFile("oem-error.txt");
        var assembly = CliFixture.CliAssemblyPath();
        var command = $"chcp 866 > \"{page}\" && dotnet \"{assembly}\" species --database \"{directory}\" > \"{output}\" 2> \"{error}\"";
        var start = new ProcessStartInfo("cmd.exe", $"/d /c {command}")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = CliFixture.Shared.Temp,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("could not start private console");
        try
        {
            Assert.True(process.WaitForExit((int)DotnetProcess.DefaultTimeout.TotalMilliseconds), "private console timed out");
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("866", File.ReadAllText(page), StringComparison.Ordinal);
            return File.ReadAllBytes(output);
        }
        catch (Exception primary)
        {
            var cleanup = Cleanup(process);
            if (cleanup is not null)
            {
                primary.Data["CleanupException"] = cleanup;
            }

            throw;
        }
    }

    private static Exception? Cleanup(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit((int)DotnetProcess.CleanupTimeout.TotalMilliseconds))
                {
                    return new TimeoutException("private console did not exit after cleanup");
                }
            }

            return null;
        }
        catch (Exception cleanup) when (cleanup is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return cleanup;
        }
    }
}
