using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Execution;

namespace APThermo.Cli.Tests;

/// <summary>L0 and L1: the four exit codes in-process; the third one as a process, in ProcessTests.</summary>
[Collection(CliCollectionDefinition.Name)]
public sealed class ExitCodeTests(CliFixture fixture)
{
    /// <summary>Relative slack on a mass read back from a message (as InputDocumentTests.GramsTolerance): the message rounds it to about 7 significant figures.</summary>
    private const double GramsTolerance = 1e-6;

    /// <summary>
    /// Absolute slack, kg, on the mass of the record of another simulation scaled by a factor, against the factor:
    /// the record weighs 1000.015 g, so scaled by 1.02 it weighs 1.0200153 kg; 1e-3 covers that and stays far below
    /// the 1 % steps the test tells apart.
    /// </summary>
    private const double ScaledMassTolerance = 1e-3;

    /// <summary>A good document is exit 0 and writes the document to the output path.</summary>
    [Fact]
    public void AGoodDocumentIsExit0AndWritesTheDocumentToTheOutputPath()
    {
        var output = fixture.TempFile("ok.json");
        var run = CliFixture.Invoke(fixture.Solving("rocket", CliFixture.Document("rocket-lox-lh2.json"), "--output", output));
        Assert.Equal(0, run.Code);
        Assert.Empty(run.Output);
        Assert.Empty(run.Error);
        using var document = JsonDocument.Parse(File.ReadAllText(output));
        var cases = document.RootElement.GetProperty("cases");
        Assert.Equal(1, cases.GetArrayLength());
        Assert.Equal("ok", cases[0].GetProperty("status").GetString());
        Assert.All(cases[0].GetProperty("stations").EnumerateArray(), s => Assert.Equal("ok", s.GetProperty("status").GetString()));
    }

    /// <summary>A failing case is exit 1 with the status in the document.</summary>
    [Fact]
    public void AFailingCaseIsExit1WithTheStatusInTheDocument()
    {
        var (code, document, _) = fixture.Produce("rocket", "rocket-failing.json");
        Assert.Equal(1, code);
        var c = document.RootElement.GetProperty("cases")[0];
        Assert.NotEqual("ok", c.GetProperty("status").GetString());
        var stations = c.GetProperty("stations").EnumerateArray().ToList();
        Assert.Equal("exit1", stations[2].GetProperty("name").GetString());
        Assert.Equal("areaRatioInvalid", stations[2].GetProperty("status").GetString());
        Assert.Equal("ok", stations[0].GetProperty("status").GetString());
    }

    /// <summary>The document type must match the command.</summary>
    [Fact]
    public void TheDocumentTypeMustMatchTheCommand()
    {
        var run = CliFixture.Invoke(fixture.Solving("rocket", CliFixture.Document("equilibrium-hp.json")));
        Assert.Equal(2, run.Code);
        Assert.Contains("run apthermo equilibrium", run.Error);
        run = CliFixture.Invoke(fixture.Solving("equilibrium", CliFixture.Document("rocket-lox-lh2.json")));
        Assert.Equal(2, run.Code);
        Assert.Contains("run apthermo rocket", run.Error);
    }

    /// <summary>A missing input file or database directory is exit 2.</summary>
    [Fact]
    public void AMissingInputFileOrDatabaseDirectoryIsExit2()
    {
        var run = CliFixture.Invoke(fixture.Solving("rocket", fixture.TempFile("nowhere.json")));
        Assert.Equal(2, run.Code);
        Assert.Contains("input file not found", run.Error);
        run = CliFixture.Invoke("rocket", CliFixture.Document("rocket-lox-lh2.json"), "--database", fixture.TempFile("nowhere"), "--accelerator", "cpu");
        Assert.Equal(2, run.Code);
        Assert.Contains("thermo.inp", run.Error);
    }

    /// <summary>A missing output directory is exit 2.</summary>
    [Fact]
    public void AMissingOutputDirectoryIsExit2()
    {
        var output = fixture.TempFile(Path.Combine("nowhere", "out.json"));
        var run = CliFixture.Invoke(fixture.Solving("rocket", CliFixture.Document("rocket-lox-lh2.json"), "--output", output));
        Assert.Equal(2, run.Code);
        Assert.Contains(output, run.Error);
        Assert.Contains("directory not found", run.Error);
        Assert.False(File.Exists(output));
    }

    /// <summary>Transport on a database without the transport file is exit 2.</summary>
    [Fact]
    public void TransportOnADatabaseWithoutTheTransportFileIsExit2()
    {
        var directory = Directory.CreateDirectory(fixture.TempFile("thermo-only")).FullName;
        File.Copy(Path.Combine(fixture.DatabasePath, "thermo.inp"), Path.Combine(directory, "thermo.inp"), true);
        var run = CliFixture.Invoke("rocket", CliFixture.Document("rocket-lox-lh2.json"), "--database", directory, "--accelerator", "cpu");
        Assert.Equal(2, run.Code);
        Assert.Contains("trans.inp", run.Error);
        run = CliFixture.Invoke("equilibrium", CliFixture.Document("equilibrium-hp.json"), "--database", directory, "--accelerator", "cpu");
        Assert.Equal(0, run.Code);
        using var document = run.Json();
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("run").GetProperty("database").GetProperty("transSha256").ValueKind);
    }

    /// <summary>A record that weighs one kilogram is exit 0 and one that does not is named by its line.</summary>
    [Fact]
    public void ARecordThatWeighsOneKilogramIsExit0AndOneThatDoesNotIsNamedByItsLine()
    {
        // The record another simulation handed over (1000.015 g with the database's atomic weights) solves.
        var (code, document, _) = fixture.Produce("states", "states-ap-al-record.json");
        Assert.Equal(0, code);
        var solved = Assert.Single(document.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal("ok", solved.GetProperty("status").GetString());

        // The same record doubled, with exits, behind the good one in a JSON Lines file: it is the first case of the rocket batch, and
        // the message names the file and the line, not the position in the batch.
        var good = JsonNode.Parse(File.ReadAllText(CliFixture.Document("states-ap-al-record.json")))![0]!;
        var doubled = JsonNode.Parse(File.ReadAllText(CliFixture.Document(Path.Combine("invalid", "states-two-kilograms.json"))))![0]!.AsObject();
        doubled["areaRatios"] = new JsonArray(10.0);
        var lines = fixture.TempFile("mixed.jsonl");
        File.WriteAllText(lines, good.ToJsonString() + "\n" + doubled.ToJsonString() + "\n");
        var run = CliFixture.Invoke(fixture.Solving("states", lines));
        Assert.Equal(2, run.Code);
        var prefix = $"{lines}:2: the composition weighs ";
        Assert.Contains(prefix, run.Error);
        var start = run.Error.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var end = run.Error.IndexOf(" g with the database's atomic weights", start, StringComparison.Ordinal);
        Assert.True(end > start, $"no ' g with the database's atomic weights' after the record's source in: {run.Error}");
        var reported = double.Parse(run.Error[start..end], CultureInfo.InvariantCulture);
        // Read numerically rather than matched as text (InputDocumentTests.AssertMassReported): the message rounds
        // the mass it reports, so a full-precision, independently derived mass matches it only up to a relative tolerance.
        var expectedGrams = fixture.GramsOf(CliFixture.CompositionOf(doubled["composition"]!));
        Assert.True(Math.Abs(reported - expectedGrams) <= GramsTolerance * Math.Max(1.0, Math.Abs(expectedGrams)), $"reported {reported:R} g, derived {expectedGrams:R} g");
        Assert.Empty(run.Output);
    }

    /// <summary>The mass tolerance option is the tolerance the run declares.</summary>
    [Fact]
    public void TheMassToleranceOptionIsTheToleranceTheRunDeclares()
    {
        // The record of another simulation made 2 % heavy: refused at the default, solved at 3 %; made 5 % heavy, refused at 3 %, naming it.
        // Heavy, not light: made light it does not converge (the front door tests node's RejectionTests say why).
        var record = JsonNode.Parse(File.ReadAllText(CliFixture.Document("states-ap-al-record.json")))![0]!.AsObject();
        string Scaled(double factor, string name)
        {
            var scaled = JsonNode.Parse(record.ToJsonString())!.AsObject();
            var composition = scaled["composition"]!.AsObject();
            foreach (var symbol in composition.Select(p => p.Key).ToList())
            {
                composition[symbol] = composition[symbol]!.GetValue<double>() * factor;
            }

            var path = fixture.TempFile(name);
            File.WriteAllText(path, scaled.ToJsonString());
            return path;
        }

        var heavy = Scaled(1.02, "heavy-2pct.json");
        var run = CliFixture.Invoke(fixture.Solving("states", heavy));
        Assert.Equal(2, run.Code);
        Assert.Contains("within 1 %", run.Error);
        run = CliFixture.Invoke(fixture.Solving("states", heavy, "--mass-tolerance", "0.03"));
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        using var document = run.Json();
        Assert.Equal(0.03, document.RootElement.GetProperty("run").GetProperty("massTolerance").GetDouble());
        var mass = document.RootElement.GetProperty("cases")[0].GetProperty("mixture").GetProperty("mass").GetDouble();
        Assert.True(Math.Abs(mass - 1.02) < ScaledMassTolerance, $"mass {mass:R} kg");

        run = CliFixture.Invoke(fixture.Solving("states", Scaled(1.05, "heavy-5pct.json"), "--mass-tolerance", "0.03"));
        Assert.Equal(2, run.Code);
        Assert.Contains("within 3 %", run.Error);
        Assert.Empty(run.Output);
    }

    /// <summary>An exception maps to its documented exit code.</summary>
    [Fact]
    public void AnExceptionMapsToItsDocumentedExitCode()
    {
        // The rule itself (Failures.Handle), directly: an input refusal is 2; an accelerator failure is 3 (F-CL-13),
        // so a defect of this node is never mistaken for invalid input.
        Assert.Equal(ExitCode.InvalidInput, Failures.Handle(new InputException("bad input"), TextWriter.Null));
        Assert.Equal(ExitCode.Infrastructure, Failures.Handle(new AcceleratorUnavailableException("no cuda", []), TextWriter.Null));
    }

    /// <summary>An unnamed exception is exit 3 through the unhandled exception rule.</summary>
    [Fact]
    public void AnUnnamedExceptionIsExit3ThroughTheUnhandledExceptionRule() =>
        // Failures.Unhandled is the shared tail of the exception -> exit code rule (API.md, the ⚠ of 2026-09-24):
        // Program.Run's own IOException and UnauthorizedAccessException catches call it directly, and
        // Program.Main's process-wide unhandled-exception handler calls it for everything else that escapes Run.
        // An unexpected defect of this node (InvalidOperationException) used to reach Failures.Handle's catch-all;
        // it now belongs here.
        Assert.Equal(ExitCode.Infrastructure, Failures.Unhandled(new InvalidOperationException("a defect of this node"), TextWriter.Null));

    /// <summary>A <see cref="TextWriter"/> that fails every write, to make Run throw an exception outside its four documented catches.</summary>
    private sealed class ThrowingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value) => throw new InvalidOperationException("a defect of this node");
    }

    /// <summary>An exception outside the four documented types leaves run in process.</summary>
    [Fact]
    public void AnExceptionOutsideTheFourDocumentedTypesLeavesRunInProcess() =>
        // API.md, the ⚠ of 2026-09-24: "an in-process caller of Run, the tests, now receives the unexpected
        // exception instead of the code 3." --version writes through the given output writer inside Run's try
        // block; a writer that throws something Run does not catch proves the exception is not swallowed.
        _ = Assert.Throws<InvalidOperationException>(() => Program.Run(["--version"], new ThrowingWriter(), TextWriter.Null));

    /// <summary>Devices is exit 0 whether or not cuda is available.</summary>
    [Fact]
    public void DevicesIsExit0WhetherOrNotCudaIsAvailable()
    {
        var run = CliFixture.Invoke("devices");
        Assert.Equal(0, run.Code);
        using var document = run.Json();
        Assert.Equal("cpu", document.RootElement.GetProperty("cpu").GetProperty("kind").GetString());
        Assert.True(document.RootElement.TryGetProperty("cuda", out _));
    }
}
