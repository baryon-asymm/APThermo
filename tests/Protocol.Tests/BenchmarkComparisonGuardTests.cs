using System.Globalization;
using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>Negative controls of the benchmark's correctness diagnostic, independent of timing or a solve.</summary>
public sealed class BenchmarkComparisonGuardTests
{
    private static readonly Type Comparison = Assembly.Load("APThermo.Benchmarks").GetType("APThermo.Benchmarks.EngineSolverComparison", throwOnError: true)!;

    /// <summary>Every numeric field refuses nonfinite corruption, including identical nonfinite raw bits.</summary>
    [Theory]
    [InlineData("APThermo.Thermo", "APThermo.Thermo.MixtureState")]
    [InlineData("APThermo.Performance", "APThermo.Performance.PerformanceFigures")]
    public void EveryComparedNumericFieldRejectsNonfiniteValues(string assembly, string typeName)
    {
        var type = Assembly.Load(assembly).GetType(typeName, throwOnError: true)!;
        var fields = type.GetProperties().Where(property => property.PropertyType == typeof(double)).ToArray();
        Assert.NotEmpty(fields);
        foreach (var field in fields)
        {
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var comparison = Create();
                CompareField(comparison, type, field, 300, invalid);
                Assert.False(Flag(comparison, "Passed"), field.Name);
                Assert.False(Flag(comparison, "ExactlyBitEqual"), field.Name);
                var sameInvalid = Create();
                CompareField(sameInvalid, type, field, invalid, invalid);
                Assert.False(Flag(sameInvalid, "Passed"), field.Name + " has identical invalid bits");
            }
        }
    }

    /// <summary>Raw signed zero is distinguishable even when a relative tolerance accepts both values.</summary>
    [Fact]
    public void ToleratedSignedZerosAreNotReportedAsBitwiseEqual()
    {
        var type = Assembly.Load("APThermo.Thermo").GetType("APThermo.Thermo.MixtureState", throwOnError: true)!;
        var field = type.GetProperty("Temperature")!;
        var comparison = Create();
        CompareField(comparison, type, field, 0.0, -0.0);
        Assert.True(Flag(comparison, "Passed"));
        Assert.False(Flag(comparison, "ExactlyBitEqual"));
    }

    /// <summary>A permitted sub-floor difference is still a difference in raw bits.</summary>
    [Fact]
    public void ToleratedTraceDifferencesAreNotReportedAsBitwiseEqual()
    {
        var comparison = Create();
        CompareFraction(comparison, 0, new Dictionary<string, double> { ["H2"] = double.Epsilon });
        Assert.True(Flag(comparison, "Passed"));
        Assert.False(Flag(comparison, "ExactlyBitEqual"));
    }

    /// <summary>A missing fraction is a mismatch even when its expected fraction is below the print floor.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void MissingFractionsAreMismatches(double expected)
    {
        var comparison = Create();
        CompareFraction(comparison, expected, []);
        Assert.False(Flag(comparison, "Passed"));
        Assert.False(Flag(comparison, "ExactlyBitEqual"));
    }

    /// <summary>Finite exact equality is a positive control, not merely absence of a failing mutation.</summary>
    [Fact]
    public void FiniteEqualValuesPassWithExactBits()
    {
        var type = Assembly.Load("APThermo.Thermo").GetType("APThermo.Thermo.MixtureState", throwOnError: true)!;
        var comparison = Create();
        CompareField(comparison, type, type.GetProperty("Temperature")!, 300, 300);
        CompareFraction(comparison, 1, new Dictionary<string, double> { ["H2"] = 1 });
        Assert.True(Flag(comparison, "Passed"));
        Assert.True(Flag(comparison, "ExactlyBitEqual"));
    }

    private static object Create()
    {
        var table = Assembly.Load("APThermo.Fixtures").GetType("APThermo.Fixtures.ToleranceTable", throwOnError: true)!;
        var tolerances = table.GetMethod("Load", Type.EmptyTypes)!.Invoke(null, null);
        return Activator.CreateInstance(Comparison, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: [tolerances], culture: CultureInfo.InvariantCulture)!;
    }

    private static bool Flag(object comparison, string name) => (bool)Comparison.GetProperty(name)!.GetValue(comparison)!;

    private static void CompareField(object comparison, Type type, PropertyInfo field, double expected, double actual)
    {
        var left = Activator.CreateInstance(type)!;
        var right = Activator.CreateInstance(type)!;
        field.SetValue(left, expected);
        field.SetValue(right, actual);
        var method = Comparison.GetMethod("CompareStructFields", BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(type);
        _ = method.Invoke(comparison, [new[] { field }, left, right, 0, 0, "guard"]);
    }

    private static void CompareFraction(object comparison, double expected, Dictionary<string, double> actual)
    {
        var method = Comparison.GetMethod("CompareMoleFraction", BindingFlags.Instance | BindingFlags.NonPublic)!;
        _ = method.Invoke(comparison, ["H2", expected, actual, 0, 0]);
    }
}
