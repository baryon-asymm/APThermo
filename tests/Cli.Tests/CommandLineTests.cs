using System.Globalization;
using AerospacePropellantThermodynamics.Cli.Syntax;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>L0: option parsing and the usage text.</summary>
[Collection(CliCollection.Name)]
public sealed class CommandLineTests(CliFixture fixture)
{
    [Fact]
    public void No_command_is_exit_2_with_the_usage()
    {
        var run = fixture.Invoke();
        Assert.Equal(2, run.Code);
        Assert.Contains("usage:", run.Error);
        Assert.Empty(run.Output);
    }

    [Fact]
    public void Help_prints_the_usage_and_exits_0()
    {
        foreach (var args in new[] { new[] { "--help" }, new[] { "-h" }, new[] { "rocket", "--help" } })
        {
            var run = fixture.Invoke(args);
            Assert.Equal(0, run.Code);
            Assert.Contains("commands:", run.Output);
            Assert.Empty(run.Error);
        }
    }

    [Theory]
    [InlineData(new[] { "frobnicate" }, "frobnicate")]
    [InlineData(new[] { "rocket", "x.json", "--bogus", "1" }, "--bogus")]
    [InlineData(new[] { "rocket" }, "exactly one")]
    [InlineData(new[] { "rocket", "a.json", "b.json" }, "exactly one")]
    [InlineData(new[] { "species", "extra" }, "no argument")]
    [InlineData(new[] { "states" }, "at least one")]
    [InlineData(new[] { "rocket", "x.json", "--format", "xml" }, "xml")]
    [InlineData(new[] { "rocket", "x.json", "--accelerator", "gpu" }, "gpu")]
    [InlineData(new[] { "rocket", "x.json", "--threshold", "-1" }, "threshold")]
    [InlineData(new[] { "rocket", "x.json", "--threshold", "many" }, "threshold")]
    [InlineData(new[] { "rocket", "x.json", "--mass-tolerance", "-0.01" }, "mass tolerance")]
    [InlineData(new[] { "states", "x.json", "--mass-tolerance", "inf" }, "mass tolerance")]
    [InlineData(new[] { "species", "--mass-tolerance", "0.1" }, "--mass-tolerance")]
    [InlineData(new[] { "species", "--transport" }, "--transport")]
    [InlineData(new[] { "rocket", "x.json", "--find", "H2" }, "--find")]
    [InlineData(new[] { "devices", "--format", "csv" }, "CSV")]
    [InlineData(new[] { "rocket", "x.json", "--output" }, "needs a value")]
    [InlineData(new[] { "rocket", "x.json", "--output", "a", "--output", "b" }, "twice")]
    [InlineData(new[] { "states", "x.json", "--transport=yes" }, "no value")]
    public void Invalid_command_lines_are_exit_2_naming_the_offender(string[] args, string fragment)
    {
        var run = fixture.Invoke(args);
        Assert.Equal(2, run.Code);
        Assert.Contains(fragment, run.Error);
        Assert.Empty(run.Output);
    }

    [Fact]
    public void The_usage_names_every_command_and_option()
    {
        foreach (var command in CommandTable.Names)
        {
            Assert.Contains($"  {command}", CommandTable.Usage);
        }

        foreach (var option in new[] { "--output", "--format", "--accelerator", "--database", "--threshold", "--mass-tolerance", "--transport", "--find", "--help" })
        {
            Assert.Contains(option, CommandTable.Usage);
        }

        Assert.Contains("exit codes: 0", CommandTable.Usage);
    }

    [Fact]
    public void Options_may_be_given_with_an_equals_sign()
    {
        var invocation = CommandLine.Parse(["rocket", "p.json", "--format=csv", "--threshold=1e-3", "--mass-tolerance=0.03", "--accelerator=cpu"]);
        Assert.Equal("rocket", invocation.Command);
        Assert.Equal(["p.json"], invocation.Arguments);
        Assert.Equal(OutputFormat.Csv, invocation.Options.Format);
        Assert.Equal(1e-3, invocation.Options.Threshold);
        Assert.Equal(0.03, invocation.Options.MassTolerance);
        Assert.Equal(Execution.AcceleratorKind.Cpu, invocation.Options.Accelerator);
        Assert.Equal(CommandOptions.DefaultThreshold, CommandLine.Parse(["devices"]).Options.Threshold);
        Assert.Equal(Problems.ElementalMixture.DefaultMassTolerance, CommandLine.Parse(["states", "r.json"]).Options.MassTolerance);
    }

    [Fact]
    public void The_usage_states_the_library_defaults()
    {
        // The usage text reads the numbers from the same constants the parser defaults to (F-AR-04), not a second typing of them.
        Assert.Contains($"default {CommandOptions.DefaultThreshold.ToString(CultureInfo.InvariantCulture)}", CommandTable.Usage);
        Assert.Contains($"default {Problems.ElementalMixture.DefaultMassTolerance.ToString(CultureInfo.InvariantCulture)}", CommandTable.Usage);
    }

    [Fact]
    public void Every_command_of_the_table_has_a_handler()
    {
        // CommandTable (what the parser accepts) and CommandRegistry (what dispatches) are two tables that could drift
        // apart; a command accepted by the first but missing from the second would fail here as "unknown command".
        foreach (var command in CommandTable.Names)
        {
            var args = command is "species" or "devices" ? new[] { command } : new[] { command, fixture.TempFile("missing.json") };
            var run = fixture.Invoke(args);
            Assert.DoesNotContain("unknown command", run.Error);
        }
    }
}
