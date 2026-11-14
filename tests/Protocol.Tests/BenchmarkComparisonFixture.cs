using System.Reflection;
using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Problems;

namespace APThermo.Protocol.Tests;

/// <summary>A real single-case CPU engine/consumer pair, assembled from the benchmark's committed fixture.</summary>
internal sealed class BenchmarkComparisonFixture
{
    private static readonly Assembly Benchmarks = Assembly.Load("APThermo.Benchmarks");

    /// <summary>Computes the pair once; no Benchmark method or timing measurement is invoked.</summary>
    public BenchmarkComparisonFixture()
    {
        var database = SpeciesDatabase.LoadBundled();
        var path = RepositoryPaths.Resolve("tests", "Fixtures", "cases", "rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium.json");
        var inputs = CeaFixtures.Load(path).Inputs;
        var (elements, _) = ((IReadOnlyList<string>, double[]))ReadFixture("FixtureJson", "ReadElementMoles", inputs);
        var species = (IReadOnlyList<string>)ReadFixture("FixtureJson", "ReadStrings", inputs, "products");
        var conditions = ReadFixture("FixtureStateRecords", "RocketBatchInputs", inputs);
        var composition = (Dictionary<string, double>)ReadFixture("FixtureJson", "ReadComposition", inputs);
        var mixture = ElementalMixture.Create(composition, Read<double>(conditions, "Enthalpy"), only: species);
        // Both routes start from the same representable input: public mol/kg to raw kmol/kg.
        // Using the original pre-conversion fixture vector instead would add a unit-roundtrip ULP difference.
        var molesPerKilogram = elements.Select(element => mixture.ElementMoles[element] * 1.0e-3).ToArray();
        var problem = new RocketProblem
        {
            ChamberPressure = Read<double>(conditions, "Pressure"),
            Flow = Read<FlowModel>(conditions, "Flow"),
            AreaRatios = Read<IReadOnlyList<double>>(conditions, "AreaRatios"),
        };
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cpu };
        using var solver = Solver.Create(database, options);
        Results = solver.Solve(mixture, [problem]);
        var tableType = Assembly.Load("APThermo.Thermo").GetType("APThermo.Thermo.SpeciesTable", throwOnError: true)!;
        var table = tableType.GetMethod("Build")!.Invoke(null, [database, elements, species])!;
        Species = Read<IReadOnlyList<string>>(table, "Species");
        var batch = ReadFixture("RocketBatches", "Build", inputs, elements.Count, molesPerKilogram, 1);
        EngineResult = RunEngine(options, table, batch);
    }

    internal object EngineResult { get; }
    internal IReadOnlyList<string> Species { get; }
    internal IReadOnlyList<RocketResult> Results { get; }

    internal RocketResult WithStations(IReadOnlyList<Station> stations)
    {
        var result = Results[0];
        var constructor = typeof(RocketResult).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Any(parameter => parameter.ParameterType == typeof(IReadOnlyList<Station>)));
        var properties = typeof(RocketResult).GetProperties();
        var arguments = constructor.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(IReadOnlyList<Station>) ? stations : properties.Single(property =>
                property.PropertyType == parameter.ParameterType).GetValue(result)).ToArray();
        return (RocketResult)constructor.Invoke(arguments);
    }

    private static object RunEngine(EngineOptions options, object table, object batch)
    {
        var engineType = Assembly.Load("APThermo.Execution").GetType("APThermo.Execution.Engine", throwOnError: true)!;
        using var engine = (IDisposable)engineType.GetMethod("Create")!.Invoke(null, [options])!;
        using var tables = (IDisposable)engineType.GetMethod("Upload")!.Invoke(engine, [table, null])!;
        var run = engineType.GetMethod("Run", [tables.GetType(), batch.GetType()])!;
        return run.Invoke(engine, [tables, batch])!;
    }

    private static object ReadFixture(string type, string method, params object[] arguments) =>
        Benchmarks.GetType("APThermo.Benchmarks." + type, throwOnError: true)!.GetMethod(method)!.Invoke(null, arguments)!;

    private static T Read<T>(object subject, string name) => (T)subject.GetType().GetProperty(name)!.GetValue(subject)!;
}
