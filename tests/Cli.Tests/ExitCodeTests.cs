using System.Text.Json;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>L0 and L1: the four exit codes in-process; the third one as a process, in ProcessTests.</summary>
[Collection(CliCollection.Name)]
public sealed class ExitCodeTests(CliFixture fixture)
{
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
    public void Devices_is_exit_0_whether_or_not_cuda_is_available()
    {
        var run = fixture.Invoke("devices");
        Assert.Equal(0, run.Code);
        using var document = run.Json();
        Assert.Equal("cpu", document.RootElement.GetProperty("cpu").GetProperty("kind").GetString());
        Assert.True(document.RootElement.TryGetProperty("cuda", out _));
    }
}
