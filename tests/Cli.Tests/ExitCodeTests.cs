using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>L0 and L1: the four exit codes in-process; the third one as a process, in ProcessTests.</summary>
[Collection(CliCollection.Name)]
public sealed class ExitCodeTests(CliFixture fixture)
{
    /// <summary>Relative slack on a mass read back from a message (as InputDocumentTests.GramsTolerance): the message rounds it to about 7 significant figures.</summary>
    private const double GramsTolerance = 1e-6;

    /// <summary>Relative slack on a mass read back from a document against the factor a fixture composition was scaled by: rounding of the scaling itself, far above double's own rounding floor.</summary>
    private const double ScaledMassTolerance = 1e-3;

    [Fact]
    public void A_good_document_is_exit_0_and_writes_the_document_to_the_output_path()
    {
        var output = fixture.TempFile("ok.json");
        var run = fixture.Invoke(fixture.Solving("rocket", fixture.Document("rocket-lox-lh2.json"), "--output", output));
        Assert.Equal(0, run.Code);
        Assert.Empty(run.Output);
        Assert.Empty(run.Error);
        using var document = JsonDocument.Parse(File.ReadAllText(output));
        var cases = document.RootElement.GetProperty("cases");
        Assert.Equal(1, cases.GetArrayLength());
        Assert.Equal("ok", cases[0].GetProperty("status").GetString());
        Assert.All(cases[0].GetProperty("stations").EnumerateArray(), s => Assert.Equal("ok", s.GetProperty("status").GetString()));
    }

    [Fact]
    public void A_failing_case_is_exit_1_with_the_status_in_the_document()
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

    [Fact]
    public void The_document_type_must_match_the_command()
    {
        var run = fixture.Invoke(fixture.Solving("rocket", fixture.Document("equilibrium-hp.json")));
        Assert.Equal(2, run.Code);
        Assert.Contains("run apthermo equilibrium", run.Error);
        run = fixture.Invoke(fixture.Solving("equilibrium", fixture.Document("rocket-lox-lh2.json")));
        Assert.Equal(2, run.Code);
        Assert.Contains("run apthermo rocket", run.Error);
    }

    [Fact]
    public void A_missing_input_file_or_database_directory_is_exit_2()
    {
        var run = fixture.Invoke(fixture.Solving("rocket", fixture.TempFile("nowhere.json")));
        Assert.Equal(2, run.Code);
        Assert.Contains("input file not found", run.Error);
        run = fixture.Invoke("rocket", fixture.Document("rocket-lox-lh2.json"), "--database", fixture.TempFile("nowhere"), "--accelerator", "cpu");
        Assert.Equal(2, run.Code);
        Assert.Contains("thermo.inp", run.Error);
    }

    [Fact]
    public void Transport_on_a_database_without_the_transport_file_is_exit_2()
    {
        var directory = Directory.CreateDirectory(fixture.TempFile("thermo-only")).FullName;
        File.Copy(Path.Combine(fixture.DatabasePath, "thermo.inp"), Path.Combine(directory, "thermo.inp"), true);
        var run = fixture.Invoke("rocket", fixture.Document("rocket-lox-lh2.json"), "--database", directory, "--accelerator", "cpu");
        Assert.Equal(2, run.Code);
        Assert.Contains("trans.inp", run.Error);
        run = fixture.Invoke("equilibrium", fixture.Document("equilibrium-hp.json"), "--database", directory, "--accelerator", "cpu");
        Assert.Equal(0, run.Code);
        using var document = run.Json();
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("run").GetProperty("database").GetProperty("transSha256").ValueKind);
    }

    [Fact]
    public void A_record_that_weighs_one_kilogram_is_exit_0_and_one_that_does_not_is_named_by_its_line()
    {
        // The record another simulation handed over (1000.015 g with the database's atomic weights) solves.
        var (code, document, _) = fixture.Produce("states", "states-ap-al-record.json");
        Assert.Equal(0, code);
        var solved = Assert.Single(document.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal("ok", solved.GetProperty("status").GetString());

        // The same record doubled, with exits, behind the good one in a JSON Lines file: it is the first case of the rocket batch, and
        // the message names the file and the line, not the position in the batch.
        var good = JsonNode.Parse(File.ReadAllText(fixture.Document("states-ap-al-record.json")))![0]!;
        var doubled = JsonNode.Parse(File.ReadAllText(fixture.Document(Path.Combine("invalid", "states-two-kilograms.json"))))![0]!.AsObject();
        doubled["areaRatios"] = new JsonArray(10.0);
        var lines = fixture.TempFile("mixed.jsonl");
        File.WriteAllText(lines, good.ToJsonString() + "\n" + doubled.ToJsonString() + "\n");
        var run = fixture.Invoke(fixture.Solving("states", lines));
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

    [Fact]
    public void The_mass_tolerance_option_is_the_tolerance_the_run_declares()
    {
        // The record of another simulation made 2 % heavy: refused at the default, solved at 3 %; made 5 % heavy, refused at 3 %, naming it.
        // Heavy, not light: made light it does not converge (the front door tests node's RejectionTests say why).
        var record = JsonNode.Parse(File.ReadAllText(fixture.Document("states-ap-al-record.json")))![0]!.AsObject();
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
        var run = fixture.Invoke(fixture.Solving("states", heavy));
        Assert.Equal(2, run.Code);
        Assert.Contains("within 1 %", run.Error);
        run = fixture.Invoke(fixture.Solving("states", heavy, "--mass-tolerance", "0.03"));
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        using var document = run.Json();
        Assert.Equal(0.03, document.RootElement.GetProperty("run").GetProperty("massTolerance").GetDouble());
        var mass = document.RootElement.GetProperty("cases")[0].GetProperty("mixture").GetProperty("mass").GetDouble();
        Assert.True(Math.Abs(mass - 1.02) < ScaledMassTolerance, $"mass {mass:R} kg");

        run = fixture.Invoke(fixture.Solving("states", Scaled(1.05, "heavy-5pct.json"), "--mass-tolerance", "0.03"));
        Assert.Equal(2, run.Code);
        Assert.Contains("within 3 %", run.Error);
        Assert.Empty(run.Output);
    }

    [Fact]
    public void An_exception_maps_to_its_documented_exit_code()
    {
        // The rule itself (Failures.Handle), directly: an input refusal is 2; an accelerator failure and every
        // other, unexpected exception are 3 (F-CL-13), so a defect of this node is never mistaken for invalid input.
        Assert.Equal(ExitCode.InvalidInput, Failures.Handle(new InputException("bad input"), TextWriter.Null));
        Assert.Equal(ExitCode.Infrastructure, Failures.Handle(new AcceleratorUnavailableException("no cuda", []), TextWriter.Null));
        Assert.Equal(ExitCode.Infrastructure, Failures.Handle(new InvalidOperationException("a defect of this node"), TextWriter.Null));
    }

    [Fact]
    public void Devices_is_exit_0_whether_or_not_cuda_is_available()
    {
        var run = fixture.Invoke("devices");
        Assert.Equal(0, run.Code);
        using var document = run.Json();
        Assert.Equal("cpu", document.RootElement.GetProperty("cpu").GetProperty("kind").GetString());
        Assert.True(document.RootElement.TryGetProperty("cuda", out _));
    }
}
