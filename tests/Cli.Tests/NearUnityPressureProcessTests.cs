using System.Text.Json;

namespace APThermo.Cli.Tests;

/// <summary>The reported near-unity pressure input goes through the actual command process.</summary>
[Collection(CliFixture.CollectionName)]
[Trait("Category", "EndToEnd")]
public sealed class NearUnityPressureProcessTests
{
    /// <summary>Successful exits must not serialize nonfinite area or vacuum impulse as null.</summary>
    [Fact]
    public void NearUnityPressureCannotReturnSuccessfulNonfinitePerformance()
    {
        var input = Path.Combine(CliFixture.NodeDirectory, "controls", "near-pressure-one.json");
        var run = CliFixture.Shared.InvokeProcess(CliFixture.Shared.Solving("rocket", input));
        Assert.True(run.Code is 0 or 1, $"exit {run.Code}: {run.Error}");
        using var document = run.Json();
        var exit = document.RootElement.GetProperty("cases")[0].GetProperty("stations")[2];
        Assert.Equal("exit1", exit.GetProperty("name").GetString());
        if (run.Code == 0)
        {
            Assert.Equal("ok", exit.GetProperty("status").GetString());
            var performance = exit.GetProperty("performance");
            Assert.Equal(JsonValueKind.Number, performance.GetProperty("areaRatio").ValueKind);
            Assert.Equal(JsonValueKind.Number, performance.GetProperty("vacuumSpecificImpulse").ValueKind);
        }
        else
        {
            Assert.NotEqual("ok", exit.GetProperty("status").GetString());
        }
    }
}
