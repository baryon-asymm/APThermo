using System.Reflection;
using APThermo.Performance;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>Failed station publication covers the generated inventory of state and performance properties.</summary>
[Collection(SolverFixture.CollectionName)]
public sealed class StationFieldInventoryTests
{
    /// <summary>All live numeric members survive success and are cleared on failures except the no-gas diagnostic pair.</summary>
    [Fact]
    public void EveryGeneratedFieldObeysThePublicFailureContract()
    {
        var table = SpeciesTable.Build(SolverFixture.Shared.Database, ["H"], ["H", "H2"]);
        var states = Properties<MixtureState>();
        var figures = Properties<PerformanceFigures>();
        var staleState = Populated<MixtureState>(states);
        var staleFigures = Populated<PerformanceFigures>(figures);
        foreach (var status in Enum.GetValues<CaseStatus>())
        {
            var station = StationFactory.Create("state", new StationSlice
            {
                Table = table,
                State = staleState,
                Performance = staleFigures,
                Moles = [1.0, 2.0],
                Offset = 0,
                Status = status,
            });
            CheckFields(states, staleState, station.State, status, state: true);
            Assert.True(station.Performance.HasValue, $"{status}: performance is missing");
            CheckFields(figures, staleFigures, station.Performance.GetValueOrDefault(), status, state: false);
            Assert.True(station.MoleFractions["H2"] > 0.0);
        }
    }

    /// <summary>Unknown properties and fields cannot disappear from the generated inventory.</summary>
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

    private static T Populated<T>(PropertyInfo[] properties) where T : struct
    {
        object boxed = default(T);
        for (var index = 0; index < properties.Length; index++)
        {
            properties[index].SetValue(boxed, index + 1.0);
        }
        return (T)boxed;
    }

    private static void CheckFields<T>(PropertyInfo[] properties, T source, T actual, CaseStatus status, bool state) where T : struct
    {
        foreach (var property in properties)
        {
            var keep = status == CaseStatus.Ok || (state && status == CaseStatus.NoGasPhase && property.Name is "Temperature" or "Pressure");
            var expected = keep ? (double)property.GetValue(source)! : 0.0;
            var value = (double)property.GetValue(actual)!;
            Assert.True(value == expected, $"{status}: {typeof(T).Name}.{property.Name} expected {expected:R}, got {value:R}");
        }
    }
}
