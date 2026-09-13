using System.ComponentModel;
using System.Diagnostics;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>Lint level: the file half of the protocol, run as the linter process the loader names, in strict mode (the tree's criterion is zero warnings).</summary>
public sealed class LintTests
{
    [Fact]
    public async Task The_tree_passes_the_protocol_linter_with_no_error_and_no_warning()
    {
        var start = new ProcessStartInfo("python")
        {
            WorkingDirectory = Tree.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-X", "utf8", Path.Combine("tools", "protocol-lint", "protocol_lint.py"), ".", "--exclude", "templates", "--strict"])
        {
            start.ArgumentList.Add(argument);
        }

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception e)
        {
            Assert.Fail($"python was not found on the path ({e.Message}); the lint level needs Python 3.8+ (tests/Protocol.Tests/BOOT.md)");
            return;
        }

        Assert.NotNull(process);
        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                Assert.Fail("protocol_lint did not finish within two minutes");
            }

            var text = await output + await error;
            Assert.True(process.ExitCode == 0, $"protocol_lint exited with {process.ExitCode} (strict: a warning fails too):\n{text}");
        }
    }
}
