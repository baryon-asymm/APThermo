using System.Reflection;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Execution.Tests;

/// <summary>Identity comes from the complete producer digest and every actual supported descriptor scalar.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationIdentityTests
{
    /// <summary>A new equal descriptor and digest can reuse an observed full boundary, but a consumed miss cannot be retried.</summary>
    [Fact]
    public void EqualContentHitsAndAnInterveningMissConsumesTheEntry()
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using (var hit = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan,
                   new string("input-A".ToCharArray()), new CalibrationViews(0, 2, default)))
        {
            Assert.Equal(15, hit.FirstChunkCases());
            Assert.True(hit.ReusedPrefix);
        }

        scenario.Train();
        using var miss = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-B", scenario.Views);
        Assert.Equal(1, miss.FirstChunkCases());
        using var consumed = scenario.Begin();
        Assert.Equal(1, consumed.FirstChunkCases());
        Assert.False(consumed.ReusedPrefix);
    }

    /// <summary>The current key's independent dimensions cannot share an entry.</summary>
    [Theory]
    [InlineData("launcher")]
    [InlineData("tables")]
    [InlineData("count")]
    [InlineData("size")]
    [InlineData("budget")]
    [InlineData("input")]
    public void EveryIdentityDimensionCausesAMiss(string dimension)
    {
        using var scenario = new CalibrationScenario();
        using var otherTables = scenario.Cpu.Upload(scenario.Tables.Species);
        scenario.Train();
        static void DifferentLauncher(int cases) => GC.KeepAlive(cases);
        var launcher = dimension == "launcher" ? DifferentLauncher : scenario.Launcher;
        var tables = dimension == "tables" ? otherTables : scenario.Tables;
        var plan = dimension switch
        {
            "count" => CalibrationScenario.MakePlan(32, 16),
            "size" => CalibrationScenario.MakePlan(31, 8),
            "budget" => CalibrationScenario.MakePlan(31, 16, 250),
            _ => scenario.Plan,
        };
        using var run = scenario.Cache.Begin(launcher, tables, plan, dimension == "input" ? "input-B" : "input-A", scenario.Views);
        Assert.Equal(1, run.FirstChunkCases());
        Assert.False(run.ReusedPrefix);
    }

    /// <summary>The machine enumerates the actual integer fields, including compiler-generated backing fields.</summary>
    [Fact]
    public void EveryActualScalarParticipatesWithoutAHandwrittenFieldInventory()
    {
        using var scenario = new CalibrationScenario();
        var fields = typeof(CalibrationViews).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(int)).ToArray();
        Assert.NotEmpty(fields);
        foreach (var field in fields)
        {
            scenario.Train();
            object changed = scenario.Views;
            field.SetValue(changed, (int)field.GetValue(changed)! + 1);
            using var run = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", (CalibrationViews)changed);
            Assert.Equal(1, run.FirstChunkCases());
        }
    }

    /// <summary>A changed descriptor type, unsupported scalar or unsupported view shape is never silently omitted.</summary>
    [Fact]
    public void DescriptorShapeAndUnsupportedIdentityDeclineReuse()
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using var changedType = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", new OtherViews(0, 2));
        Assert.Equal(1, changedType.FirstChunkCases());
        AssertUnsupported(scenario, new UnsupportedScalar(0, 2L));
        AssertUnsupported(scenario, new UnsupportedView(default));
        scenario.Train();
        using var missing = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, null, scenario.Views);
        Assert.Equal(1, missing.FirstChunkCases());
    }

    /// <summary>The production entry-point descriptors are discovered from actual compiled parameter types.</summary>
    [Fact]
    public void ProductionDescriptorsIncludeEveryActualIntegerScalar()
    {
        using var scenario = new CalibrationScenario();
        var descriptors = typeof(Kernels).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType)
            .Where(type => type.IsValueType && type.Name.EndsWith("BatchViews", StringComparison.Ordinal)).Distinct().ToArray();
        Assert.NotEmpty(descriptors);
        var check = typeof(CalibrationIdentityTests).GetMethod(nameof(CheckDescriptor), BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var descriptor in descriptors)
        {
            _ = check.MakeGenericMethod(descriptor).Invoke(null, [scenario]);
        }
    }

    private static void CheckDescriptor<TViews>(CalibrationScenario scenario) where TViews : struct
    {
        TViews original = default;
        TrainDescriptor(scenario, original);
        using (var positive = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", original))
        {
            Assert.Equal(15, positive.FirstChunkCases());
        }

        foreach (var field in typeof(TViews).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(field => field.FieldType == typeof(int)))
        {
            TrainDescriptor(scenario, original);
            object changed = original;
            field.SetValue(changed, 1);
            using var miss = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", (TViews)changed);
            Assert.Equal(1, miss.FirstChunkCases());
        }
    }

    private static void TrainDescriptor<TViews>(CalibrationScenario scenario, TViews views) where TViews : struct
    {
        using var run = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", views);
        foreach (var cases in new[] { 1, 2, 4, 8, 16 })
        {
            run.ObserveSuccessfulLaunch(cases, TimeSpan.Zero);
        }

        run.Complete();
    }

    private static void AssertUnsupported<TViews>(CalibrationScenario scenario, TViews views) where TViews : struct
    {
        using (var first = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", views))
        {
            foreach (var cases in new[] { 1, 2, 4, 8, 16 })
            {
                first.ObserveSuccessfulLaunch(cases, TimeSpan.Zero);
            }

            first.Complete();
        }

        using var repeated = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", views);
        Assert.Equal(1, repeated.FirstChunkCases());
        Assert.False(repeated.ReusedPrefix);
    }

    private readonly record struct OtherViews(int Seeded, int ExitCount);
    private readonly record struct UnsupportedScalar(int Seeded, long ExitCount);
    private readonly record struct UnsupportedView(ArrayView1D<double, Stride1D.Dense> Values);
}