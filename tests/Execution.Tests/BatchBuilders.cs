using System.Diagnostics;
using System.Text.Json;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>The inputs of one rocket fixture as the solver takes them.</summary>
internal sealed record RocketInputs(
    string[] Elements, double[] ElementMoles, string[] Products, double ChamberPressure, double ReactantEnthalpy,
    FlowModel Flow, double[] ExitValues, ExitSpecification[] ExitKinds, bool Transport)
{
    public string BatchKey => string.Join(",", Elements) + "|" + string.Join(",", Products) + "|" + string.Join(",", ExitKinds);

    public static RocketInputs Of(CeaCase c)
    {
        var inputs = c.Inputs;
        var elements = inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();
        var elementMoles = inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble()).ToArray();
        var products = inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var pressureRatios = inputs.GetProperty("pressureRatios").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var areaRatios = inputs.GetProperty("areaRatios").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var flow = inputs.GetProperty("flow").GetString() switch
        {
            "shiftingEquilibrium" => FlowModel.ShiftingEquilibrium,
            "frozenAtChamber" => FlowModel.FrozenAtChamber,
            "frozenAtThroat" => FlowModel.FrozenAtThroat,
            var other => throw new ArgumentException($"unknown flow model {other}"),
        };
        return new RocketInputs(elements, elementMoles, products, inputs.GetProperty("chamberPressure").GetDouble(),
                                inputs.GetProperty("reactantEnthalpy").GetDouble(), flow,
                                pressureRatios.Concat(areaRatios).ToArray(),
                                pressureRatios.Select(_ => ExitSpecification.PressureRatio).Concat(areaRatios.Select(_ => ExitSpecification.AreaRatio)).ToArray(),
                                inputs.GetProperty("transport").GetBoolean());
    }
}

/// <summary>A family of rocket fixtures sharing one table and one exit layout, as one batch.</summary>
internal sealed record RocketFamily(string Name, SpeciesTable Table, TransportTable Transport, IReadOnlyList<string> Members, IReadOnlyList<RocketInputs> Inputs)
{
    public RocketBatch Batch()
    {
        var batch = new RocketBatch(Inputs.Count, Table.ElementCount, Inputs[0].ExitKinds);
        for (var k = 0; k < Inputs.Count; k++)
        {
            var input = Inputs[k];
            batch.ChamberPressure[k] = input.ChamberPressure;
            batch.ReactantEnthalpy[k] = input.ReactantEnthalpy;
            batch.Flow[k] = input.Flow;
            Array.Copy(input.ElementMoles, 0, batch.ElementMoles, k * Table.ElementCount, Table.ElementCount);
            Array.Copy(input.ExitValues, 0, batch.ExitValues, k * batch.Exits, batch.Exits);
        }

        return batch;
    }
}

/// <summary>Builds batches from the fixtures and solves cases one at a time on the host for comparison.</summary>
internal static class BatchBuilders
{
    /// <summary>Every rocket fixture, grouped into families; the largest family first.</summary>
    public static IReadOnlyList<RocketFamily> RocketFamilies(SpeciesDatabase database)
    {
        var groups = new Dictionary<string, (List<string> Names, List<RocketInputs> Inputs)>(StringComparer.Ordinal);
        foreach (var path in FixtureFiles.Enumerate("rocket"))
        {
            var c = CeaFixtures.Load(path);
            var inputs = RocketInputs.Of(c);
            if (!groups.TryGetValue(inputs.BatchKey, out var group))
            {
                groups[inputs.BatchKey] = group = ([], []);
            }

            group.Names.Add(c.Name);
            group.Inputs.Add(inputs);
        }

        return groups.Values
            .OrderByDescending(g => g.Names.Count).ThenBy(g => g.Names[0], StringComparer.Ordinal)
            .Select(g =>
            {
                var table = SpeciesTable.Build(database, g.Inputs[0].Elements, g.Inputs[0].Products);
                return new RocketFamily(g.Names[0], table, TransportTable.Build(database.Transport!, table), g.Names, g.Inputs);
            })
            .ToList();
    }

    /// <summary>The family names as theory data.</summary>
    public static IEnumerable<object[]> FamilyNames(SpeciesDatabase database) => RocketFamilies(database).Select(f => new object[] { f.Name });

    public static RocketFamily Family(SpeciesDatabase database, string name) => RocketFamilies(database).Single(f => f.Name == name);

    /// <summary>
    /// A parametric sweep between two fixtures of one family: element moles and reactant enthalpy interpolated linearly (a mixture of the
    /// two propellants), the chamber pressure swept between the bounds, shifting equilibrium, the exits of the family.
    /// </summary>
    public static RocketBatch Sweep(RocketFamily family, string from, string to, int count, double pressureLow, double pressureHigh)
    {
        var a = family.Inputs[family.Members.ToList().IndexOf(from)];
        var b = family.Inputs[family.Members.ToList().IndexOf(to)];
        var elementCount = family.Table.ElementCount;
        var batch = new RocketBatch(count, elementCount, a.ExitKinds);
        const int mixtureSteps = 400;
        var pressureSteps = Math.Max(1, (count + mixtureSteps - 1) / mixtureSteps);
        for (var k = 0; k < count; k++)
        {
            var t = (k % mixtureSteps) / (double)(mixtureSteps - 1);
            var u = pressureSteps == 1 ? 0.0 : (k / mixtureSteps) / (double)(pressureSteps - 1);
            batch.ChamberPressure[k] = pressureLow + u * (pressureHigh - pressureLow);
            batch.ReactantEnthalpy[k] = (1.0 - t) * a.ReactantEnthalpy + t * b.ReactantEnthalpy;
            batch.Flow[k] = FlowModel.ShiftingEquilibrium;
            for (var i = 0; i < elementCount; i++)
            {
                batch.ElementMoles[k * elementCount + i] = (1.0 - t) * a.ElementMoles[i] + t * b.ElementMoles[i];
            }

            Array.Copy(a.ExitValues, 0, batch.ExitValues, k * batch.Exits, batch.Exits);
        }

        return batch;
    }

    /// <summary>The equilibrium fixtures (tp, hp, sp) sharing one table, as one batch with the table, in file order.</summary>
    public static (EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) EquilibriumFamily(SpeciesDatabase database, string namePrefix)
    {
        var cases = new[] { "tp", "hp", "sp" }
            .SelectMany(kind => FixtureFiles.Enumerate(kind))
            .Select(CeaFixtures.Load)
            .Where(c => c.Name.StartsWith(namePrefix, StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(cases);
        var first = cases[0].Inputs;
        var elements = first.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();
        var products = first.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var table = SpeciesTable.Build(database, elements, products);
        var batch = new EquilibriumBatch(cases.Count, elements.Length);
        for (var k = 0; k < cases.Count; k++)
        {
            var c = cases[k];
            Assert.Equal(products, c.Inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray());
            batch.Kind[k] = c.Kind switch
            {
                "tp" => ProblemKind.AssignedTemperaturePressure,
                "hp" => ProblemKind.AssignedEnthalpyPressure,
                "sp" => ProblemKind.AssignedEntropyPressure,
                var other => throw new ArgumentException($"unknown kind {other}"),
            };
            batch.Pressure[k] = c.Inputs.GetProperty("pressure").GetDouble();
            batch.Temperature[k] = c.Kind == "tp" ? c.Inputs.GetProperty("temperature").GetDouble() : 0.0;
            batch.Target[k] = c.Kind switch
            {
                "hp" => c.Inputs.GetProperty("enthalpy").GetDouble(),
                "sp" => c.Inputs.GetProperty("entropy").GetDouble(),
                _ => 0.0,
            };
            var moles = c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble()).ToArray();
            Array.Copy(moles, 0, batch.ElementMoles, k * elements.Length, elements.Length);
        }

        return (batch, table, cases);
    }

    /// <summary>One rocket case solved on the host over the accelerator's buffers, as the numerical node is called directly.</summary>
    public static (MixtureState[] Stations, double[] Moles, PerformanceFigures[] Figures, CaseStatus[] StationStatus, int[] Iterations, CaseStatus Status)
        SolveRocketOnHost(Accelerator accelerator, SpeciesTableBuffers buffers, RocketBatch batch, int k)
    {
        var table = buffers.Table;
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var stationCount = batch.StationCount;
        using var elements = accelerator.Allocate1D(batch.ElementMoles.AsSpan(k * elementCount, elementCount).ToArray());
        using var exitValues = accelerator.Allocate1D(batch.Exits == 0 ? new[] { 0.0 } : batch.ExitValues.AsSpan(k * batch.Exits, batch.Exits).ToArray());
        using var exitKinds = accelerator.Allocate1D(batch.Exits == 0 ? new[] { 0 } : batch.ExitKinds.Select(x => (int)x).ToArray());
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var stations = accelerator.Allocate1D<MixtureState>(stationCount);
        using var moles = accelerator.Allocate1D<double>((long)stationCount * speciesCount);
        using var multipliers = accelerator.Allocate1D<double>((long)stationCount * elementCount);
        using var figures = accelerator.Allocate1D<PerformanceFigures>(stationCount);
        using var stationStatus = accelerator.Allocate1D<int>(stationCount);
        using var iterations = accelerator.Allocate1D<int>(stationCount);
        using var status = accelerator.Allocate1D<int>(1);
        moles.MemSetToZero();
        stations.MemSetToZero();
        figures.MemSetToZero();
        var problem = new RocketProblem(batch.ChamberPressure[k], batch.ReactantEnthalpy[k], batch.TemperatureEstimate[k], batch.Flow[k], elements.View,
                                        exitValues.View.SubView(0, batch.Exits), exitKinds.View.SubView(0, batch.Exits));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new RocketResult(stations.View, moles.View, multipliers.View, figures.View, stationStatus.View, iterations.View, status.View);
        var view = buffers.View;
        RocketSolver.Solve(in view, in problem, in scratch, in result);
        return (stations.GetAsArray1D(), moles.GetAsArray1D(), figures.GetAsArray1D(),
                stationStatus.GetAsArray1D().Select(s => (CaseStatus)s).ToArray(), iterations.GetAsArray1D(), (CaseStatus)status.GetAsArray1D()[0]);
    }

    /// <summary>One equilibrium case solved on the host.</summary>
    public static (MixtureState State, double[] Moles, CaseStatus Status, int Iterations) SolveEquilibriumOnHost(Accelerator accelerator, SpeciesTableBuffers buffers, EquilibriumBatch batch, int k)
    {
        var table = buffers.Table;
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(batch.ElementMoles.AsSpan(k * elementCount, elementCount).ToArray());
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var moles = accelerator.Allocate1D<double>(speciesCount);
        using var multipliers = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        moles.MemSetToZero();
        state.MemSetToZero();
        var problem = new EquilibriumProblem(batch.Kind[k], batch.Pressure[k], batch.Temperature[k], batch.Target[k], elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);
        var view = buffers.View;
        EquilibriumSolver.Solve(in view, in problem, in scratch, in result, false);
        return (state.GetAsArray1D()[0], moles.GetAsArray1D(), (CaseStatus)status.GetAsArray1D()[0], iterations.GetAsArray1D()[0]);
    }

    /// <summary>One station's transport evaluated on the host.</summary>
    public static (CaseStatus Status, TransportFigures Figures) EvaluateTransportOnHost(Accelerator accelerator, SpeciesTableBuffers species, TransportTableBuffers transport,
                                                                                         double temperature, double[] moles, int offset)
    {
        var speciesCount = species.Table.SpeciesCount;
        var elementCount = species.Table.ElementCount;
        using var molesBuffer = accelerator.Allocate1D(moles.AsSpan(offset, speciesCount).ToArray());
        using var doubles = accelerator.Allocate1D<double>(TransportLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        using var figures = accelerator.Allocate1D<TransportFigures>(1);
        var scratch = TransportScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var speciesView = species.View;
        var transportView = transport.View;
        var status = TransportSolver.Evaluate(in speciesView, in transportView, temperature, molesBuffer.View, in scratch, figures.View);
        return (status, figures.GetAsArray1D()[0]);
    }

    public static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    /// <summary>Field-by-field bit equality of two structs.</summary>
    public static IEnumerable<string> BitDifferences<T>(T expected, T actual, string label) where T : struct
    {
        foreach (var field in typeof(T).GetFields())
        {
            var a = field.GetValue(expected)!;
            var b = field.GetValue(actual)!;
            var same = a is double x && b is double y ? SameBits(x, y) : a.Equals(b);
            if (!same)
            {
                yield return $"{label} {field.Name}: {a} vs {b}";
            }
        }
    }
}

/// <summary>The long-running sweep: the same batch on the CPU accelerator and on CUDA (twice), with the times.</summary>
internal sealed record SweepRun(RocketBatch Batch, RocketBatchResult Cpu, RocketBatchResult? Cuda, RocketBatchResult? CudaAgain, TimeSpan CpuSeconds, TimeSpan CudaSeconds)
{
    public const int LongRunningCases = 100_000;
    public const string FamilyName = "lox-lh2_of4_pc10MPa_frozenAtChamber";
    public const string From = "lox-lh2_of4_pc7MPa_shiftingEquilibrium";
    public const string To = "lox-lh2_of8_pc7MPa_shiftingEquilibrium";

    public static SweepRun Run(EngineFixture fixture, int count)
    {
        var family = BatchBuilders.Family(fixture.Database, FamilyName);
        var batch = BatchBuilders.Sweep(family, From, To, count, 5.0e6, 10.0e6);
        using var cpuTables = fixture.Cpu.Upload(family.Table);
        fixture.Cpu.Run(cpuTables, BatchBuilders.Sweep(family, From, To, 64, 5.0e6, 10.0e6));   // warm-up
        var watch = Stopwatch.StartNew();
        var cpu = fixture.Cpu.Run(cpuTables, batch);
        var cpuSeconds = watch.Elapsed;
        RocketBatchResult? cuda = null;
        RocketBatchResult? again = null;
        var cudaSeconds = TimeSpan.Zero;
        if (fixture.Cuda is { } engine)
        {
            using var cudaTables = engine.Upload(family.Table);
            engine.Run(cudaTables, BatchBuilders.Sweep(family, From, To, 64, 5.0e6, 10.0e6));   // warm-up
            watch.Restart();
            cuda = engine.Run(cudaTables, batch);
            cudaSeconds = watch.Elapsed;
            again = engine.Run(cudaTables, batch);
        }

        return new SweepRun(batch, cpu, cuda, again, cpuSeconds, cudaSeconds);
    }
}
