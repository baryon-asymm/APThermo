using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// An equilibrium family whose batch is seeded by moles (2026-10-04): one table, one seeded batch, the labels of its cases, the fixture
/// cases whose reference temperature the CPU accelerator is checked against (null where the inputs are computed), the temperature of the
/// plateau every case lies on (null where there is none), and whether a case's <c>Iterations</c> sums the attempts of the temperature
/// bracket (<see cref="GpuCpuComparison.IterationsSumAttempts"/>).
/// </summary>
internal sealed record SeededFamily(SpeciesTable Table, EquilibriumBatch Batch, IReadOnlyList<string> Labels, IReadOnlyList<CeaCase>? References,
                                    double? PlateauTemperature, bool SumsAttempts);

/// <summary>
/// The seeded equilibrium families (<see cref="BatchTests"/>, <see cref="CudaTests"/>): every fixture of the <c>seeded</c> kind started from
/// the tp moles at its seed temperature; the tp fixtures of every table solved cold and again at half the pressure from their own moles; and
/// the bracketed plateau states seeded 20 K above the plateau (<see cref="GasPlateauFamilies.SeededKeys"/>). Every seed is the CPU
/// accelerator's own result, handed identically to both accelerators, so a CUDA comparison compares the seeded solve alone.
/// </summary>
internal static class SeededFamilies
{
    /// <summary>The name of the family of the <c>seeded</c> fixtures.</summary>
    public const string Fixtures = "seeded-fixtures";

    /// <summary>The prefix of the names of the warm-start families.</summary>
    private const string WarmPrefix = "warm-half-pressure-";

    /// <summary>The pressure of a warm-start case, as a share of the pressure its seed was solved at.</summary>
    private const double HalfPressure = 0.5;

    /// <summary>The fixture kind of the seeded fixtures.</summary>
    private const string SeededKind = "seeded";

    /// <summary>The names of the seeded families as theory data.</summary>
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string> { Fixtures };
        foreach (var table in FixtureBatches.EquilibriumTableNames("tp"))
        {
            data.Add(WarmPrefix + table);
        }

        foreach (var plateau in GasPlateauFamilies.SeededKeys)
        {
            data.Add(plateau);
        }

        return data;
    }

    /// <summary>One seeded family by its name.</summary>
    public static SeededFamily Family(SpeciesDatabase database, string name)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(name);
        if (name == Fixtures)
        {
            return FixtureFamily(database);
        }

        if (name.StartsWith(WarmPrefix, StringComparison.Ordinal))
        {
            return WarmFamily(database, name);
        }

        var plateau = GasPlateauFamilies.Family(database, name);
        return new SeededFamily(plateau.Table, plateau.Batch, plateau.Labels, null, plateau.PlateauTemperature, SumsAttempts: true);
    }

    /// <summary>
    /// Every fixture of the <c>seeded</c> kind, one table: a cold tp batch at each case's seed temperature and pressure gives the seeds, every
    /// case <c>Ok</c>, and the family is the hp or sp state of the case with the seed temperature as its estimate.
    /// </summary>
    private static SeededFamily FixtureFamily(SpeciesDatabase database)
    {
        var cases = CeaFixtures.LoadAll(SeededKind);
        Assert.NotEmpty(cases);
        var key = FixtureBatches.TableKey(cases[0]);
        Assert.All(cases, c => Assert.Equal(key, FixtureBatches.TableKey(c)));
        var elements = cases[0].Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();
        var products = cases[0].Inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var table = SpeciesTable.Build(database, elements, products);
        var seeds = new EquilibriumBatch(cases.Count, elements.Length);
        var batch = new EquilibriumBatch(cases.Count, elements.Length, table.SpeciesCount);
        for (var k = 0; k < cases.Count; k++)
        {
            var inputs = cases[k].Inputs;
            var enthalpy = inputs.TryGetProperty("enthalpy", out var h);
            var temperature = inputs.GetProperty("seed").GetProperty("temperature").GetDouble();
            var pressure = inputs.GetProperty("pressure").GetDouble();
            var moles = inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble()).ToArray();
            seeds.Kind[k] = ProblemKind.AssignedTemperaturePressure;
            seeds.Pressure[k] = pressure;
            seeds.Temperature[k] = temperature;
            batch.Kind[k] = enthalpy ? ProblemKind.AssignedEnthalpyPressure : ProblemKind.AssignedEntropyPressure;
            batch.Pressure[k] = pressure;
            batch.Temperature[k] = temperature;
            batch.Target[k] = enthalpy ? h.GetDouble() : inputs.GetProperty("entropy").GetDouble();
            Array.Copy(moles, 0, seeds.ElementMoles, k * elements.Length, elements.Length);
            Array.Copy(moles, 0, batch.ElementMoles, k * elements.Length, elements.Length);
        }

        Seed(table, seeds, batch, [.. cases.Select(c => c.Name)]);
        return new SeededFamily(table, batch, [.. cases.Select(c => c.Name)], cases, null, SumsAttempts: false);
    }

    /// <summary>
    /// The tp fixtures of one table, those the CPU accelerator solves <c>Ok</c> from its own estimate, again at half the pressure, each
    /// started from the moles of its first solve: the warm start of a tp case, which no rocket station takes.
    /// </summary>
    private static SeededFamily WarmFamily(SpeciesDatabase database, string name)
    {
        var table = name[WarmPrefix.Length..];
        var (source, speciesTable, cases) = FixtureBatches.EquilibriumTableFamily(database, table);
        var tp = Enumerable.Range(0, source.Count).Where(k => source.Kind[k] == ProblemKind.AssignedTemperaturePressure).ToArray();
        var cold = Rows(source, tp);
        using var tables = EngineFixture.Shared.Cpu.Upload(speciesTable);
        var run = EngineFixture.Shared.Cpu.Run(tables, cold);
        var kept = Enumerable.Range(0, tp.Length).Where(i => run.Status[i] == CaseStatus.Ok).ToArray();
        Assert.NotEmpty(kept);
        var seeds = Rows(cold, kept);
        var batch = new EquilibriumBatch(kept.Length, source.ElementCount, speciesTable.SpeciesCount);
        for (var k = 0; k < kept.Length; k++)
        {
            batch.Kind[k] = seeds.Kind[k];
            batch.Pressure[k] = HalfPressure * seeds.Pressure[k];
            batch.Temperature[k] = seeds.Temperature[k];
            Array.Copy(seeds.ElementMoles, k * source.ElementCount, batch.ElementMoles, k * source.ElementCount, source.ElementCount);
            Array.Copy(run.Moles, kept[k] * speciesTable.SpeciesCount, batch.SeedMoles!, k * speciesTable.SpeciesCount, speciesTable.SpeciesCount);
        }

        return new SeededFamily(speciesTable, batch, [.. kept.Select(i => cases[tp[i]].Name + " at half pressure")], null, null, SumsAttempts: false);
    }

    /// <summary>Runs the cold tp batch on the CPU accelerator, asserts every case <c>Ok</c> and copies each case's moles into the seed row of the family.</summary>
    private static void Seed(SpeciesTable table, EquilibriumBatch cold, EquilibriumBatch family, IReadOnlyList<string> labels)
    {
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var run = EngineFixture.Shared.Cpu.Run(tables, cold);
        for (var k = 0; k < cold.Count; k++)
        {
            Assert.True(run.Status[k] == CaseStatus.Ok, $"{labels[k]}: the seed state ends {run.Status[k]}");
        }

        Array.Copy(run.Moles, family.SeedMoles!, run.Moles.Length);
    }

    /// <summary>A cold batch of the given rows of another, in the given order.</summary>
    private static EquilibriumBatch Rows(EquilibriumBatch batch, int[] sources)
    {
        var rows = new EquilibriumBatch(sources.Length, batch.ElementCount);
        for (var k = 0; k < sources.Length; k++)
        {
            rows.Kind[k] = batch.Kind[sources[k]];
            rows.Pressure[k] = batch.Pressure[sources[k]];
            rows.Temperature[k] = batch.Temperature[sources[k]];
            rows.Target[k] = batch.Target[sources[k]];
            Array.Copy(batch.ElementMoles, sources[k] * batch.ElementCount, rows.ElementMoles, k * batch.ElementCount, batch.ElementCount);
        }

        return rows;
    }
}
