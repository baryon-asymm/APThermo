using System.Globalization;
using System.Text.Json;
using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Thermo;
using ILGPU.Runtime.Cuda;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>The exact frozen P1 inputs, including seeded phase reversals, across actual accelerator backends.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class AuditPhaseCudaTests(ITestOutputHelper output)
{
    private sealed record CaseRow(ProblemKind Kind, double Temperature, double Target, string Label);

    private static readonly JsonElement Cases = LoadCases();

    /// <summary>The CPU companion proves the frozen inputs execute and retain their scientific obligations without CUDA.</summary>
    /// <param name="name">The frozen chemical system.</param>
    [Theory]
    [InlineData("srcl2")]
    [InlineData("multiFloor")]
    public void FrozenPhaseInputsExerciseColdSeededAndRocketCpuPrograms(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var cpu = EngineFixture.Shared.Cpu;
        Assert.Equal(AcceleratorKind.Cpu, cpu.Accelerator.Kind);
        var input = Cases.GetProperty(name);
        var table = Table(input);
        using var tables = cpu.Upload(table);
        var rows = Rows(input);
        var coldBatch = Batch(input, table, rows);
        var cold = cpu.Run(tables, coldBatch);
        AssertEquilibrium(input, table, rows, coldBatch, cold);
        var seededBatch = Batch(input, table, rows, cold);
        var seeded = cpu.Run(tables, seededBatch);
        var seedCase = input.TryGetProperty("phaseChecks", out _) ? rows.Count - 1 : 0;
        output.WriteLine("PHASE_WITNESS " + JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            system = name,
            fixture = "tests/Fixtures/AuditPhaseCases.json",
            rawDataSha256 = EngineFixture.Shared.Database.Provenance.ThermoSha256,
            backend = cpu.Accelerator,
            input,
            species = table.Species,
            elements = table.Elements,
            rows = rows.Select(row => new { kind = row.Kind.ToString(), row.Temperature, row.Target, row.Label }),
            seedCase,
            seedState = cold.State[seedCase],
            seedMoles = cold.Moles.Skip(seedCase * table.SpeciesCount).Take(table.SpeciesCount).ToArray(),
            seededBatch = new { kinds = seededBatch.Kind.Select(kind => kind.ToString()), seededBatch.Pressure, seededBatch.Temperature, seededBatch.Target, seededBatch.ElementMoles, seededBatch.SeedMoles },
            cold = new { statuses = cold.Status.Select(status => status.ToString()), cold.State, cold.Moles, cold.Iterations },
            seeded = new { statuses = seeded.Status.Select(status => status.ToString()), seeded.State, seeded.Moles, seeded.Iterations },
        }));
        AssertEquilibrium(input, table, rows, seededBatch, seeded);
        var rocketBatch = Rocket(input, table);
        AssertRocket(input, table, rocketBatch, cpu.Run(tables, rocketBatch));
        output.WriteLine($"CPU P1 controls: {name}; cold={rows.Count}, seeded={rows.Count}, rocket=1; no CUDA evidence");
    }

    /// <summary>Every status, state, amount, figure and work counter matches for identical cold/seeded/rocket inputs.</summary>
    /// <param name="name">The frozen chemical system.</param>
    [Theory]
    [InlineData("srcl2")]
    [InlineData("multiFloor")]
    [Trait("Category", "Cuda")]
    [Trait("CudaEvidence", "hardware")]
    public void ActualCudaMatchesTheCpuOnEveryFrozenPhaseInput(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var cuda = EngineFixture.Shared.RequireCuda(output);
        if (cuda is null)
        {
            output.WriteLine($"actualCuda=false; {name}: explicit forbidden-CUDA refusal control only; no hardware pair executed");
            return;
        }

        Assert.Equal(AcceleratorKind.Cuda, cuda.Accelerator.Kind);
        Assert.Null(cuda.Accelerator.CudaSkippedBecause);
        var actualDevice = Assert.IsType<CudaAccelerator>(cuda.IlgpuAccelerator);
        Assert.False(string.IsNullOrWhiteSpace(actualDevice.Name));
        output.WriteLine($"actualCuda=true; system={name}; deviceId={actualDevice.DeviceId}; name={actualDevice.Name}; multiprocessors={actualDevice.NumMultiprocessors}; ILGPU={cuda.Accelerator.IlgpuVersion}");
        var cpu = EngineFixture.Shared.Cpu;
        Assert.Equal(AcceleratorKind.Cpu, cpu.Accelerator.Kind);
        var input = Cases.GetProperty(name);
        var table = Table(input);
        using var cpuTables = cpu.Upload(table);
        using var cudaTables = cuda.Upload(table);
        var rows = Rows(input);
        var coldBatch = Batch(input, table, rows);
        var coldCpu = cpu.Run(cpuTables, coldBatch);
        var coldCuda = cuda.Run(cudaTables, coldBatch);
        Assert.Equal(AcceleratorKind.Cpu, coldCpu.Accelerator.Kind);
        Assert.Equal(cuda.Accelerator, coldCuda.Accelerator);
        AssertEquilibrium(input, table, rows, coldBatch, coldCpu);
        AssertEquilibrium(input, table, rows, coldBatch, coldCuda);
        Assert.Empty(ExactComparison.Equilibrium(coldCpu, coldCuda, index => $"{name} cold {rows[index].Label}"));
        // Both backends receive the same explicit seed bytes. Different seeds need not pick identical TP fractions at a crossing.
        var seededBatch = Batch(input, table, rows, coldCpu);
        var seededCpu = cpu.Run(cpuTables, seededBatch);
        var seededCuda = cuda.Run(cudaTables, seededBatch);
        Assert.Equal(cuda.Accelerator, seededCuda.Accelerator);
        AssertEquilibrium(input, table, rows, seededBatch, seededCpu);
        AssertEquilibrium(input, table, rows, seededBatch, seededCuda);
        Assert.Empty(ExactComparison.Equilibrium(seededCpu, seededCuda, index => $"{name} seeded {rows[index].Label}"));
        var rocketBatch = Rocket(input, table);
        var rocketCpu = cpu.Run(cpuTables, rocketBatch);
        var rocketCuda = cuda.Run(cudaTables, rocketBatch);
        Assert.Equal(cuda.Accelerator, rocketCuda.Accelerator);
        AssertRocket(input, table, rocketBatch, rocketCpu);
        AssertRocket(input, table, rocketBatch, rocketCuda);
        Assert.Empty(ExactComparison.Rocket(rocketCpu, rocketCuda, _ => $"{name} rocket"));
        output.WriteLine($"actualCuda=true; completed {name}: cold={rows.Count}, seeded={rows.Count}, rocket=1; exact states/compositions/statuses/iterations/figures");
        CudaEvidence.CompleteHardware(output, cuda.Accelerator.DeviceName);
    }

    private static SpeciesTable Table(JsonElement input) => SpeciesTable.Build(EngineFixture.Shared.Database,
        [.. input.GetProperty("elementMoles").EnumerateObject().Select(property => property.Name)],
        [.. input.GetProperty("products").EnumerateArray().Select(value => value.GetString()!)]);

    private static List<CaseRow> Rows(JsonElement input)
    {
        var rows = new List<CaseRow>();
        if (input.TryGetProperty("phaseChecks", out var points))
        {
            foreach (var point in points.EnumerateArray())
            {
                rows.Add(new CaseRow(Kind: ProblemKind.AssignedTemperaturePressure, Temperature: Number(point, "temperature"), Target: 0, Label: $"tp {Number(point, "temperature"):R} K"));
            }

            rows.Add(new CaseRow(Kind: ProblemKind.AssignedEnthalpyPressure, Temperature: 0, Target: Number(input, "enthalpy"), Label: "hp default estimate"));
        }
        else
        {
            foreach (var kind in new[] { "hp", "sp" })
            {
                var constraint = input.GetProperty(kind);
                var temperature = Number(constraint.GetProperty("witness"), "temperature");
                rows.Add(new CaseRow(Kind: ProblemKind.AssignedTemperaturePressure, Temperature: temperature, Target: 0, Label: $"tp {kind} feasible witness"));
                var problemKind = kind == "hp" ? ProblemKind.AssignedEnthalpyPressure : ProblemKind.AssignedEntropyPressure;
                rows.Add(new CaseRow(Kind: problemKind, Temperature: 0, Target: Number(constraint, "target"), Label: $"{kind} default estimate"));
                rows.Add(new CaseRow(Kind: problemKind, Temperature: temperature, Target: Number(constraint, "target"), Label: $"{kind} witness estimate"));
            }
        }

        return rows;
    }

    private static EquilibriumBatch Batch(JsonElement input, SpeciesTable table, IReadOnlyList<CaseRow> rows, EquilibriumBatchResult? seed = null)
    {
        var batch = seed is null ? new EquilibriumBatch(rows.Count, table.ElementCount) : new EquilibriumBatch(rows.Count, table.ElementCount, table.SpeciesCount);
        var seedCase = input.TryGetProperty("phaseChecks", out _) ? rows.Count - 1 : 0;
        for (var index = 0; index < rows.Count; index++)
        {
            batch.Kind[index] = rows[index].Kind;
            batch.Pressure[index] = Number(input, "pressure");
            batch.Temperature[index] = rows[index].Temperature;
            batch.Target[index] = rows[index].Target;
            FillElements(input, table, batch.ElementMoles, index);
            if (seed is not null)
            {
                Array.Copy(seed.Moles, seedCase * table.SpeciesCount, batch.SeedMoles!, index * table.SpeciesCount, table.SpeciesCount);
            }
        }

        return batch;
    }

    private static RocketBatch Rocket(JsonElement input, SpeciesTable table)
    {
        var pressureExits = input.TryGetProperty("pressureRatios", out var pressures);
        var values = pressureExits ? pressures : input.GetProperty("areaRatios");
        var kinds = Enumerable.Repeat(pressureExits ? ExitSpecification.PressureRatio : ExitSpecification.AreaRatio, values.GetArrayLength()).ToArray();
        var batch = new RocketBatch(1, table.ElementCount, kinds);
        batch.ChamberPressure[0] = Number(input, "pressure");
        batch.ReactantEnthalpy[0] = input.TryGetProperty("enthalpy", out var enthalpy) ? enthalpy.GetDouble() : Number(input.GetProperty("hp"), "target");
        batch.Flow[0] = FlowModel.ShiftingEquilibrium;
        FillElements(input, table, batch.ElementMoles, 0);
        for (var exit = 0; exit < batch.Exits; exit++)
        {
            batch.ExitValues[exit] = values[exit].GetDouble();
        }

        return batch;
    }

    private static void FillElements(JsonElement input, SpeciesTable table, double[] destination, int caseIndex)
    {
        for (var element = 0; element < table.ElementCount; element++)
        {
            destination[caseIndex * table.ElementCount + element] = input.GetProperty("elementMoles").GetProperty(table.Elements[element]).GetDouble() / 1000;
        }
    }

    private static void AssertEquilibrium(JsonElement input, SpeciesTable table, IReadOnlyList<CaseRow> rows, EquilibriumBatch batch, EquilibriumBatchResult result)
    {
        var balance = new ElementBalance(table, batch.ElementMoles);
        for (var index = 0; index < rows.Count; index++)
        {
            Assert.Equal(CaseStatus.Ok, result.Status[index]);
            Assert.Empty(balance.Exceeding(balance.Residuals(index, result.Moles, index), "accelerator", rows[index].Label));
            var state = result.State[index];
            Assert.Equal(Number(input, "pressure"), state.Pressure);
            if (rows[index].Kind == ProblemKind.AssignedTemperaturePressure)
            {
                Assert.Equal(rows[index].Temperature, state.Temperature);
                if (input.TryGetProperty("phaseChecks", out var points))
                {
                    var point = points[index];
                    var total = result.Moles.Skip(index * table.SpeciesCount).Take(table.SpeciesCount).Sum();
                    Assert.True(result.Moles[index * table.SpeciesCount + table.IndexOf(point.GetProperty("alternative").GetString()!)] > 0);
                    Assert.True(result.Moles[index * table.SpeciesCount + table.IndexOf(point.GetProperty("presentInOldRepro").GetString()!)] <= total * EngineFixture.Shared.Tolerances.For("moleFractionFloor").Absolute);
                }
            }
            else
            {
                var kind = rows[index].Kind == ProblemKind.AssignedEnthalpyPressure ? "hp" : "sp";
                AssertClose(rows[index].Target, kind == "hp" ? state.Enthalpy : state.Entropy, rows[index].Label);
                if (input.TryGetProperty(kind, out var constraint))
                {
                    AssertObjective(kind, constraint.GetProperty("witness"), state);
                }
            }
        }
    }

    private static void AssertRocket(JsonElement input, SpeciesTable table, RocketBatch batch, RocketBatchResult result)
    {
        Assert.Equal(CaseStatus.Ok, result.StationStatus[0]);
        AssertClose(batch.ReactantEnthalpy[0], result.Stations[0].Enthalpy, "rocket chamber H");
        if (input.TryGetProperty("hp", out var constraint))
        {
            AssertObjective("hp", constraint.GetProperty("witness"), result.Stations[0]);
        }

        var balance = new ElementBalance(table, batch.ElementMoles);
        for (var station = 0; station < result.StationCount; station++)
        {
            if (result.StationStatus[station] == CaseStatus.Ok)
            {
                Assert.Empty(balance.Exceeding(balance.Residuals(0, result.Moles, station), "accelerator", $"rocket station {station}"));
                AssertClose(result.Stations[0].Entropy, result.Stations[station].Entropy, $"rocket station {station} S");
            }
        }

        if (result.Status[0] == CaseStatus.Ok)
        {
            Assert.All(result.StationStatus, status => Assert.Equal(CaseStatus.Ok, status));
        }
    }

    private static void AssertObjective(string kind, JsonElement witness, MixtureState state)
    {
        var value = Number(witness, kind == "hp" ? "independentEntropy" : "independentEnthalpy");
        Assert.True(kind == "hp" ? state.Entropy + Budget(value) >= value : state.Enthalpy <= value + Budget(value), $"{kind} state T={state.Temperature:R}, H={state.Enthalpy:R}, S={state.Entropy:R} loses to frozen independent objective {value:R}");
    }

    private static double Budget(double value) => EngineFixture.Shared.Tolerances.For("polishThresholdRelative").Relative * Math.Max(1, Math.Abs(value));

    private static void AssertClose(double expected, double actual, string label) =>
        Assert.True(double.IsFinite(actual) && Math.Abs(actual - expected) <= Budget(expected), $"{label}: expected {expected:R}, actual {actual:R}");

    private static double Number(JsonElement input, string name)
    {
        var value = input.GetProperty(name);
        return value.ValueKind == JsonValueKind.String ? double.Parse(value.GetString()!, CultureInfo.InvariantCulture) : value.GetDouble();
    }

    private static JsonElement LoadCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Resolve("tests", "Fixtures", "AuditPhaseCases.json")));
        Assert.Equal(EngineFixture.SharedDatabase.Provenance.ThermoSha256, document.RootElement.GetProperty("provenance").GetProperty("rawDataSha256").GetString());
        return document.RootElement.Clone();
    }
}