using System.Reflection;
using System.Text.Json;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>Controls CUDA evidence and the generated inventory of CUDA-tagged facts.</summary>
public sealed class CudaEvidenceTests(ITestOutputHelper output)
{
    /// <summary>Every CUDA-tagged method has exactly one supported scope in its discovered traits.</summary>
    [Fact]
    public void EveryCudaFactHasOneMachineDiscoverableScope()
    {
        var inventory = typeof(EngineFixture).Assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(method => (Method: method, Traits: method.CustomAttributes.Where(attribute => attribute.AttributeType == typeof(TraitAttribute)).ToArray()))
            .Where(item => item.Traits.Any(trait => Trait(trait, "Category", "Cuda")))
            .Select(item => (Name: item.Method.DeclaringType!.FullName + "." + item.Method.Name,
                Scopes: item.Traits.Where(trait => (trait.ConstructorArguments[0].Value as string) == "CudaEvidence")
                    .Select(trait => trait.ConstructorArguments[1].Value as string).ToArray()))
            .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(inventory);
        foreach (var (name, scopes) in inventory)
        {
            Assert.True(scopes.Length == 1 && (scopes[0] is "hardware" or "driver-only"),
                $"{name}: expected exactly one hardware or driver-only CudaEvidence trait");
        }
        output.WriteLine("CUDA_EVIDENCE_INVENTORY:" + JsonSerializer.Serialize(inventory.Select(item => new { name = item.Name, scope = item.Scopes[0] })));
    }

    /// <summary>A failed refusal assertion never produces a verified refusal marker.</summary>
    [Fact]
    public void RefusalIsReportedOnlyAfterItsAssertionSucceeds()
    {
        var captured = new CapturedOutput();
        _ = Assert.Throws<InvalidOperationException>(() => CudaEvidence.Refusal(captured, () => throw new InvalidOperationException("not refused")));
        Assert.Empty(captured.Lines);
        CudaEvidence.Refusal(captured, () => { });
        using var value = Marker(Assert.Single(captured.Lines));
        Assert.Equal("hardware", value.RootElement.GetProperty("scope").GetString());
        Assert.False(value.RootElement.GetProperty("actualCuda").GetBoolean());
        Assert.True(value.RootElement.GetProperty("refusalVerified").GetBoolean());
        Assert.False(value.RootElement.GetProperty("scenarioExecuted").GetBoolean());
    }

    /// <summary>A selected real device is distinguishable from driver-only execution and an omitted scenario.</summary>
    [Fact]
    public void MarkersDescribeSelectedDeviceAndDriverScope()
    {
        var captured = new CapturedOutput();
        CudaEvidence.Hardware(captured, "test device | 0");
        CudaEvidence.DriverExecuted(captured);
        CudaEvidence.NotRun(captured, "driver-only");
        Assert.Equal(3, captured.Lines.Count);
        using var hardware = Marker(captured.Lines[0]);
        Assert.Equal("hardware", hardware.RootElement.GetProperty("scope").GetString());
        Assert.True(hardware.RootElement.GetProperty("actualCuda").GetBoolean());
        Assert.Equal("test device | 0", hardware.RootElement.GetProperty("device").GetString());
        using var driver = Marker(captured.Lines[1]);
        Assert.Equal("driver-only", driver.RootElement.GetProperty("scope").GetString());
        Assert.False(driver.RootElement.GetProperty("actualCuda").GetBoolean());
        Assert.False(driver.RootElement.GetProperty("refusalVerified").GetBoolean());
        Assert.True(driver.RootElement.GetProperty("scenarioExecuted").GetBoolean());
        using var absent = Marker(captured.Lines[2]);
        Assert.False(absent.RootElement.GetProperty("scenarioExecuted").GetBoolean());
        Assert.False(absent.RootElement.GetProperty("refusalVerified").GetBoolean());
    }

    private static bool Trait(CustomAttributeData trait, string name, string value) =>
        trait.ConstructorArguments.Count == 2 && (trait.ConstructorArguments[0].Value as string) == name &&
        (trait.ConstructorArguments[1].Value as string) == value;

    private static JsonDocument Marker(string text)
    {
        Assert.StartsWith(CudaEvidence.Prefix, text, StringComparison.Ordinal);
        return JsonDocument.Parse(text[CudaEvidence.Prefix.Length..]);
    }

    private sealed class CapturedOutput : ITestOutputHelper
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
    }
}
