using System.Reflection;
using System.Text.Json.Nodes;
using APThermo.Performance;
using APThermo.Thermo;

namespace APThermo.Cli.Tests;

/// <summary>Failed station postconditions remain visible in the real in-process JSON command.</summary>
[Collection(CliFixture.CollectionName)]
public sealed class FailedStationOutputTests
{
    /// <summary>The known frozen exit failure keeps preceding stations and emits zero numeric fields.</summary>
    [Fact]
    public void FrozenExitFailureReportsZeroStateAndPerformance()
    {
        var input = JsonNode.Parse(File.ReadAllText(CliFixture.Document("rocket-lox-lh2.json")))!.AsObject();
        var problem = input["problem"]!.AsObject();
        problem["chamberPressure"] = 1000000.0;
        problem["flow"] = "frozen-at-chamber";
        problem["areaRatios"] = new JsonArray(300.0, 1800.0, 6000.0, 10518.632521348223);
        problem["transport"] = false;
        var path = CliFixture.Shared.TempFile("failed-frozen-station.json");
        File.WriteAllText(path, input.ToJsonString());
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("rocket", path));
        Assert.Equal(1, run.Code);
        using var document = run.Json();
        var stations = document.RootElement.GetProperty("cases")[0].GetProperty("stations");
        Assert.All(stations.EnumerateArray().Take(stations.GetArrayLength() - 1), station => Assert.Equal("ok", station.GetProperty("status").GetString()));
        var failed = stations[stations.GetArrayLength() - 1];
        Assert.Equal("temperatureOutOfRange", failed.GetProperty("status").GetString());
        AssertZeroFields<MixtureState>(failed, "state");
        AssertZeroFields<PerformanceFigures>(failed.GetProperty("performance"), "performance");
    }

    /// <summary>Non-numeric additions to the published shape require an explicit JSON rule.</summary>
    [Fact]
    public void InventoryRejectsNewUnsupportedMembers()
    {
        Assert.Equal(1.0, new SupportedShape { Value = 1.0 }.Value);
        _ = Assert.Single(Properties<SupportedShape>());
        Assert.True(new UnsupportedProperty { Added = true }.Added);
        var property = Assert.Throws<InvalidOperationException>(Properties<UnsupportedProperty>);
        Assert.Contains("UnsupportedProperty.Added", property.Message, StringComparison.Ordinal);
        Assert.True(new UnsupportedField { Added = true }.Added);
        var field = Assert.Throws<InvalidOperationException>(Properties<UnsupportedField>);
        Assert.Contains("UnsupportedField.Added", field.Message, StringComparison.Ordinal);
    }

    private static PropertyInfo[] Properties<T>()
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (!property.CanRead || !property.CanWrite || property.PropertyType != typeof(double) || property.GetIndexParameters().Length != 0)
            {
                throw new InvalidOperationException($"Unsupported public property {typeof(T).Name}.{property.Name}: {property.PropertyType}");
            }
        }
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        if (fields.Length > 0)
        {
            throw new InvalidOperationException($"Unsupported public field {typeof(T).Name}.{fields[0].Name}: {fields[0].FieldType}");
        }
        Assert.NotEmpty(properties);
        return properties;
    }

    private sealed class SupportedShape { public double Value { get; set; } }
    private sealed class UnsupportedProperty { public double Value { get; set; } public bool Added { get; set; } }
    private sealed class UnsupportedField { public double Value { get; set; } public bool Added; }

    private static void AssertZeroFields<T>(System.Text.Json.JsonElement element, string group)
    {
        var properties = Properties<T>();
        foreach (var property in properties)
        {
            Assert.Equal(typeof(double), property.PropertyType);
            var name = CliFixture.Camel(property.Name);
            var value = element.GetProperty(name).GetDouble();
            Assert.True(value == 0.0, $"{group}.{name}: expected 0, got {value:R}");
        }
    }
}
