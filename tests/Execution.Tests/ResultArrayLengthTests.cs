using System.Reflection;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>Physical storage lengths are compared even when the logical result dimensions agree.</summary>
public sealed class ResultArrayLengthTests
{
    /// <summary>Every array independent of Count, with both a missing element and an extra tail.</summary>
    public static TheoryData<string, string, int> LengthChanges
    {
        get
        {
            var rows = new TheoryData<string, string, int>();
            foreach (var (kind, countArray) in CountArrays())
            {
                foreach (var member in Create(kind).GetType().GetProperties().Where(property => property.PropertyType.IsArray && property.Name != countArray))
                {
                    rows.Add(kind, member.Name, -1);
                    rows.Add(kind, member.Name, 1);
                }
            }

            return rows;
        }
    }

    /// <summary>A length mismatch names its array and both physical lengths, without indexing beyond either side.</summary>
    [Theory]
    [MemberData(nameof(LengthChanges))]
    public void APhysicalLengthDifferenceIsRefusedAtEqualLogicalDimensions(string kind, string memberName, int difference)
    {
        var expected = Create(kind);
        var original = Create(kind);
        var member = expected.GetType().GetProperty(memberName)!;
        var before = Assert.IsType<Array>(member.GetValue(original), exactMatch: false);
        var resized = Array.CreateInstance(member.PropertyType.GetElementType()!, before.Length + difference);
        Array.Copy(before, resized, Math.Min(before.Length, resized.Length));
        var actual = WithArray(original, memberName, resized);
        var dimensions = expected.GetType().GetProperties().Where(property => property.PropertyType == typeof(int)).ToArray();
        Assert.NotEmpty(dimensions);
        foreach (var dimension in dimensions)
        {
            Assert.Equal(dimension.GetValue(expected), dimension.GetValue(actual));
        }

        var mismatch = Assert.Single(Compare(expected, actual));
        Assert.Contains(memberName, mismatch, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"length: expected {before.Length}, actual {resized.Length}", mismatch, StringComparison.Ordinal);
    }

    /// <summary>Independent normally shaped results of each kind still compare cleanly.</summary>
    [Theory]
    [InlineData("Equilibrium")]
    [InlineData("Rocket")]
    [InlineData("Transport")]
    [InlineData("Functions")]
    public void EqualPhysicalLengthsCompareCleanly(string kind) => Assert.Empty(Compare(Create(kind), Create(kind)));

    /// <summary>Value, struct and array-length diagnostics share one limit, including the last struct's members.</summary>
    [Fact]
    public void ArrayLengthAndStructDifferencesRespectTheRemainingMessageBudget()
    {
        var count = ExactComparison.Shown;
        var accelerator = new AcceleratorInfo(AcceleratorKind.Cpu, "test", "test", 1);
        var timings = new RunTimings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        var expected = new EquilibriumBatchResult(speciesCount: 1, state: new MixtureState[count], moles: new double[count],
            status: new CaseStatus[count], iterations: new int[count], timings: timings, accelerator: accelerator);
        var states = new MixtureState[count];
        states[0] = new MixtureState { Temperature = 1.0, Pressure = 2.0 };
        var statuses = new CaseStatus[count];
        Array.Fill(statuses, CaseStatus.InvalidInput, 0, count - 1);
        var actual = new EquilibriumBatchResult(speciesCount: 1, state: states, moles: new double[count + 1],
            status: statuses, iterations: new int[count], timings: timings, accelerator: accelerator);
        var mismatches = ExactComparison.Equilibrium(expected, actual, index => $"case {index}");
        Assert.Equal(ExactComparison.Shown, mismatches.Count);
        Assert.Contains(mismatches, message => message.Contains("Temperature", StringComparison.Ordinal));
    }

    private static Dictionary<string, string> CountArrays() => new(StringComparer.Ordinal)
    {
        ["Equilibrium"] = nameof(EquilibriumBatchResult.State),
        ["Rocket"] = nameof(RocketBatchResult.Status),
        ["Transport"] = nameof(TransportBatchResult.Status),
        ["Functions"] = nameof(SpeciesFunctionBatchResult.CpOverR),
    };

    private static object Create(string kind)
    {
        var accelerator = new AcceleratorInfo(AcceleratorKind.Cpu, "test", "test", 1);
        var timings = new RunTimings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        return kind switch
        {
            "Equilibrium" => new EquilibriumBatchResult(speciesCount: 2, state: new MixtureState[2], moles: [1.0, 2.0, 3.0, 4.0],
                status: [CaseStatus.Ok, CaseStatus.NoGasPhase], iterations: [1, 2], timings: timings, accelerator: accelerator),
            "Rocket" => new RocketBatchResult(speciesCount: 2, stationCount: 2, stations: new MixtureState[4],
                moles: [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0], figures: new PerformanceFigures[4],
                stationStatus: [CaseStatus.Ok, CaseStatus.Ok, CaseStatus.NoGasPhase, CaseStatus.Ok], iterations: [1, 2, 3, 4],
                status: [CaseStatus.Ok, CaseStatus.Ok], timings: timings, accelerator: accelerator),
            "Transport" => new TransportBatchResult(new TransportFigures[2], [CaseStatus.Ok, CaseStatus.NoGasPhase], timings, accelerator),
            "Functions" => new SpeciesFunctionBatchResult([1.0, 2.0], [3.0, 4.0], [5.0, 6.0], [true, false], timings, accelerator),
            _ => throw new ArgumentException("Unknown result kind", nameof(kind)),
        };
    }

    private static object WithArray(object result, string memberName, Array replacement)
    {
        var type = result.GetType();
        var constructor = Assert.Single(type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        var arguments = constructor.GetParameters().Select(parameter =>
        {
            var property = type.GetProperty(parameter.Name!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)!;
            return property.Name == memberName ? replacement : property.GetValue(result);
        }).ToArray();
        return constructor.Invoke(arguments);
    }

    private static List<string> Compare(object expected, object actual) => expected switch
    {
        EquilibriumBatchResult equilibrium => ExactComparison.Equilibrium(equilibrium, (EquilibriumBatchResult)actual, index => $"case {index}"),
        RocketBatchResult rocket => ExactComparison.Rocket(rocket, (RocketBatchResult)actual, index => $"case {index}"),
        TransportBatchResult transport => ExactComparison.Transport(transport, (TransportBatchResult)actual),
        SpeciesFunctionBatchResult functions => ExactComparison.Functions(functions, (SpeciesFunctionBatchResult)actual, index => $"entry {index}"),
        _ => throw new ArgumentException("Unknown result type", nameof(expected)),
    };
}
