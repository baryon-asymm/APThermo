using System.Globalization;
using APThermo.Data;
using APThermo.Equilibrium.Condensed;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>Generate overlap families from the database rather than claiming coverage from a typed species list.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class OverlappingPhaseInventoryTests
{
    /// <summary>Every distinct-name, same-formula condensed overlap inside the solver's mixture window.</summary>
    public static TheoryData<string, string, double> Overlaps()
    {
        var data = new TheoryData<string, string, double>();
        var groups = CpuFixture.Shared.Database.Products
            .Where(s => s.Phase == SpeciesPhase.Condensed && s.Intervals.Count > 0)
            .GroupBy(FormulaKey);
        foreach (var group in groups)
        {
            var records = group.ToArray();
            for (var j = 0; j < records.Length; j++)
            {
                AddPairs(records, j, data);
            }
        }

        Assert.NotEmpty(data);
        return data;
    }

    private static void AddPairs(Species[] records, int first, TheoryData<string, string, double> data)
    {
        var left = records[first];
        for (var second = first + 1; second < records.Length; second++)
        {
            var right = records[second];
            var low = Math.Max(EquilibriumSolver.MinMixtureTemperature,
                Math.Max(left.Intervals.Min(i => i.TLow), right.Intervals.Min(i => i.TLow)));
            var high = Math.Min(EquilibriumSolver.MaxMixtureTemperature,
                Math.Min(left.Intervals.Max(i => i.THigh), right.Intervals.Max(i => i.THigh)));
            if (left.Name != right.Name && high > low)
            {
                data.Add(left.Name, right.Name, low + (high - low) * 0.5);
            }
        }
    }

    /// <summary>From the less stable held record, one set update chooses the lower potential or leaves equality alone.</summary>
    [Theory]
    [MemberData(nameof(Overlaps))]
    public void AnEligibleOverlapCannotKeepTheHigherPotentialRecord(string first, string second, double temperature)
    {
        var database = CpuFixture.Shared.Database;
        var elements = database[first].Formula.Select(e => e.Symbol).ToArray();
        var gas = database.Products.First(s => s.Phase == SpeciesPhase.Gas && s.Formula.Count > 0
            && s.Formula.All(e => elements.Contains(e.Symbol, StringComparer.Ordinal)));
        var table = SpeciesTable.Build(database, elements, [gas.Name, first, second]);
        CheckUpdate(table, temperature);
    }

    private static void CheckUpdate(SpeciesTable table, double temperature)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var elements = accelerator.Allocate1D(Enumerable.Repeat(1.0, table.ElementCount).ToArray());
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(table.SpeciesCount, table.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(table.SpeciesCount, table.ElementCount));
        using var moles = accelerator.Allocate1D<double>(table.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(table.ElementCount);
        using var states = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1.0e5, temperature, 0.0, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, table.SpeciesCount, table.ElementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, states.View, status.View, iterations.View);
        var state = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(buffers.View, problem, scratch, result, EstimateSource.Defaults, ref state));
        var eligible = Enumerable.Range(table.GasCount, table.CondensedCount)
            .Where(j => PhaseGeometry.InEffectiveRange(buffers.View, scratch, j, temperature)).ToArray();
        Assert.NotEmpty(eligible);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.GOverRT[j] = SpeciesFunctions.GOverRT(buffers.View, j, temperature);
        }

        var worst = eligible.MaxBy(j => scratch.GOverRT[j]);
        var best = eligible.MinBy(j => scratch.GOverRT[j]);
        state.CondensedCount = 1;
        scratch.CondensedInSolution[0] = worst;
        result.Moles[worst] = 1.0;
        var gain = scratch.GOverRT[worst] - scratch.GOverRT[best];
        var changed = CondensedSet.Update(buffers.View, problem, scratch, result, ref state);
        Assert.Equal(gain > Tolerances.ResidualInclusionGain, changed);
        Assert.True(scratch.GOverRT[scratch.CondensedInSolution[0]] <= scratch.GOverRT[best] + Tolerances.ResidualInclusionGain);
    }

    private static string FormulaKey(Species species) =>
        string.Join("|", species.Formula.OrderBy(e => e.Symbol, StringComparer.Ordinal)
            .Select(e => string.Create(CultureInfo.InvariantCulture, $"{e.Symbol}:{e.Count:R}")));
}
