using System.Reflection;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>Proves each reflected numerical result member affects the exact comparison.</summary>
public sealed class ResultComparisonCoverageTests
{
    /// <summary>Every result array and every nested numerical field rejects an isolated change.</summary>
    [Fact]
    public void EveryNumericalMemberDetectsAnIndependentChange()
    {
        var cases = Cases();
        var discovered = typeof(Engine).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == "Run").Select(method => method.ReturnType).ToHashSet();
        Assert.True(discovered.SetEquals(cases.Keys));
        foreach (var (type, compare) in cases)
        {
            var (expected, actual, check) = compare();
            Assert.Empty(check(expected, actual));
            var arrays = Members(type).Where(member => MemberType(member).IsArray).ToArray();
            Assert.NotEmpty(arrays);
            foreach (var member in arrays)
            {
                var array = Assert.IsType<Array>(Read(member, actual), exactMatch: false);
                Assert.True(array.Length > 0, $"{type.Name}.{member.Name} is empty");
                CheckArray(type, member, array, expected, actual, check);
            }
        }
    }

    /// <summary>Every reflected scalar dimension is guarded by the result shape check.</summary>
    [Fact]
    public void EveryScalarDimensionRejectsAnotherShape()
    {
        var accelerator = new AcceleratorInfo(AcceleratorKind.Cpu, "test", "test", 1);
        var timings = new RunTimings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        var equilibrium = new EquilibriumBatchResult(speciesCount: 1, state: [new MixtureState()], moles: [1.0], status: [CaseStatus.Ok], iterations: [1], timings: timings, accelerator: accelerator);
        var rocket = new RocketBatchResult(speciesCount: 1, stationCount: 1, stations: [new MixtureState()], moles: [1.0], figures: [new PerformanceFigures()], stationStatus: [CaseStatus.Ok], iterations: [1], status: [CaseStatus.Ok], timings: timings, accelerator: accelerator);
        var transport = new TransportBatchResult([new TransportFigures()], [CaseStatus.Ok], timings, accelerator);
        var functions = new SpeciesFunctionBatchResult([1.0], [1.0], [1.0], [true], timings, accelerator);
        Assert.NotEmpty(ExactComparison.Equilibrium(equilibrium, new(2, [new MixtureState()], [1.0], [CaseStatus.Ok], [1], timings, accelerator), i => $"{i}"));
        Assert.NotEmpty(ExactComparison.Equilibrium(equilibrium, new(1, [new MixtureState(), new MixtureState()], [1.0], [CaseStatus.Ok], [1], timings, accelerator), i => $"{i}"));
        Assert.NotEmpty(ExactComparison.Rocket(rocket, new(2, 1, [new MixtureState()], [1.0], [new PerformanceFigures()], [CaseStatus.Ok], [1], [CaseStatus.Ok], timings, accelerator), i => $"{i}"));
        Assert.NotEmpty(ExactComparison.Rocket(rocket, new(1, 2, [new MixtureState()], [1.0], [new PerformanceFigures()], [CaseStatus.Ok], [1], [CaseStatus.Ok], timings, accelerator), i => $"{i}"));
        Assert.NotEmpty(ExactComparison.Rocket(rocket, new(1, 1, [new MixtureState()], [1.0], [new PerformanceFigures()], [CaseStatus.Ok], [1], [CaseStatus.Ok, CaseStatus.Ok], timings, accelerator), i => $"{i}"));
        Assert.NotEmpty(ExactComparison.Transport(transport, new([new TransportFigures()], [CaseStatus.Ok, CaseStatus.Ok], timings, accelerator)));
        Assert.NotEmpty(ExactComparison.Functions(functions, new([1.0, 1.0], [1.0], [1.0], [true], timings, accelerator), i => $"{i}"));
        var scalarCounts = Cases().Keys.SelectMany(Members).Count(member => MemberType(member) == typeof(int));
        Assert.Equal(7, scalarCounts);
    }

    private static void CheckArray(Type type, MemberInfo member, Array array, object expected, object actual, Func<object, object, List<string>> check)
    {
        var original = array.GetValue(0)!;
        var element = MemberType(member).GetElementType()!;
        if (element.IsPrimitive || element.IsEnum)
        {
            array.SetValue(Different(original), 0);
            Assert.True(check(expected, actual).Count > 0, $"{type.Name}.{member.Name} was ignored");
            array.SetValue(original, 0);
            return;
        }

        var fields = Members(element);
        Assert.NotEmpty(fields);
        foreach (var field in fields)
        {
            var changed = array.GetValue(0)!;
            Write(field, changed, Different(Read(field, changed)!));
            array.SetValue(changed, 0);
            Assert.True(check(expected, actual).Count > 0, $"{type.Name}.{member.Name}.{field.Name} was ignored");
            array.SetValue(original, 0);
        }
    }

    private static MemberInfo[] Members(Type type) =>
        [.. type.GetFields(BindingFlags.Instance | BindingFlags.Public), .. type.GetProperties(BindingFlags.Instance | BindingFlags.Public)];

    private static Type MemberType(MemberInfo member) => member switch
    {
        FieldInfo field => field.FieldType,
        PropertyInfo property => property.PropertyType,
        _ => throw new InvalidOperationException("Unknown member kind"),
    };

    private static object? Read(MemberInfo member, object instance) => member switch
    {
        FieldInfo field => field.GetValue(instance),
        PropertyInfo property => property.GetValue(instance),
        _ => throw new InvalidOperationException("Unknown member kind"),
    };

    private static void Write(MemberInfo member, object instance, object value)
    {
        if (member is FieldInfo field) { field.SetValue(instance, value); }
        else if (member is PropertyInfo property) { property.SetValue(instance, value); }
        else { throw new InvalidOperationException("Unknown member kind"); }
    }

    private static object Different(object value) => value switch
    {
        double number => double.IsNaN(number) ? 1.0 : Math.BitIncrement(number),
        int number => number + 1,
        bool flag => !flag,
        CaseStatus status => status == CaseStatus.Ok ? CaseStatus.NotConverged : CaseStatus.Ok,
        _ => throw new InvalidOperationException($"No test mutation for {value.GetType()}")
    };

    private static Dictionary<Type, Func<(object Expected, object Actual, Func<object, object, List<string>> Check)>> Cases()
    {
        var accelerator = new AcceleratorInfo(AcceleratorKind.Cpu, "test", "test", 1);
        var timings = new RunTimings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        EquilibriumBatchResult Equilibrium() => new(1, [new MixtureState()], [1.0], [CaseStatus.Ok], [1], timings, accelerator);
        RocketBatchResult Rocket() => new(1, 1, [new MixtureState()], [1.0], [new PerformanceFigures()], [CaseStatus.Ok], [1], [CaseStatus.Ok], timings, accelerator);
        TransportBatchResult Transport() => new([new TransportFigures()], [CaseStatus.Ok], timings, accelerator);
        SpeciesFunctionBatchResult Functions() => new([1.0], [1.0], [1.0], [true], timings, accelerator);
        return new()
        {
            [typeof(EquilibriumBatchResult)] = () => (Equilibrium(), Equilibrium(), (a, b) => ExactComparison.Equilibrium((EquilibriumBatchResult)a, (EquilibriumBatchResult)b, i => $"{i}")),
            [typeof(RocketBatchResult)] = () => (Rocket(), Rocket(), (a, b) => ExactComparison.Rocket((RocketBatchResult)a, (RocketBatchResult)b, i => $"{i}")),
            [typeof(TransportBatchResult)] = () => (Transport(), Transport(), (a, b) => ExactComparison.Transport((TransportBatchResult)a, (TransportBatchResult)b)),
            [typeof(SpeciesFunctionBatchResult)] = () => (Functions(), Functions(), (a, b) => ExactComparison.Functions((SpeciesFunctionBatchResult)a, (SpeciesFunctionBatchResult)b, i => $"{i}")),
        };
    }
}

