using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>Negative controls and public failure cases from the 2026-11-14 audit.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class AuditGuardTests
{
    /// <summary>The two sets share a count and a sum of squares, but are different sets.</summary>
    [Fact]
    public void DifferentCondensedSetsCannotShareAnIdentity()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        var slots = context.Scratch.CondensedInSolution;
        for (var slot = 0; slot < slots.Length; slot++)
        {
            slots[slot] = -1;
        }

        slots[0] = 100;
        slots[1] = 104;
        slots[2] = 105;
        var first = ThroatBracketSearch.CondensedFingerprint(in context);
        slots[0] = 101;
        slots[1] = 102;
        slots[2] = 106;
        var second = ThroatBracketSearch.CondensedFingerprint(in context);
        Assert.NotEqual(first, second);
        slots[0] = 106;
        slots[1] = 101;
        slots[2] = 102;
        Assert.Equal(second, ThroatBracketSearch.CondensedFingerprint(in context));
        slots[3] = 106;
        Assert.Equal(second, ThroatBracketSearch.CondensedFingerprint(in context));
    }

    /// <summary>An ordinary sonic throat must still compare its numeric state and figures.</summary>
    [Fact]
    public void AnOrdinarySonicThroatRejectsCorruptedNumericFields()
    {
        var c = CeaFixtures.LoadAll("rocket").Concat(CeaFixtures.LoadAll("throat"))
            .First(c => c.Name != "beo-h2o-throat_pc15MPa_h-11.06875MJkg"
                && RocketInputs.FixtureStationsOf(c)[RocketSolver.Throat].GetProperty("mach").GetDouble() >= 1.0);
        var solution = RocketHost.Solve(CpuFixture.Shared, c);
        var tolerances = ToleranceTable.Load();
        Assert.Empty(StationComparison.Compare(c, solution, tolerances));
        var originalState = solution.Outcome.Stations[RocketSolver.Throat];
        var originalFigures = solution.Outcome.Figures[RocketSolver.Throat];
        var state = originalState;
        state.Temperature *= 2.0;
        solution.Outcome.Stations[RocketSolver.Throat] = state;
        Assert.Contains(StationComparison.Compare(c, solution, tolerances), s => s.Contains("temperature", StringComparison.Ordinal));
        state = originalState;
        state.GammaS *= 2.0;
        solution.Outcome.Stations[RocketSolver.Throat] = state;
        Assert.Contains(StationComparison.Compare(c, solution, tolerances), s => s.Contains("gammaS", StringComparison.Ordinal));
        solution.Outcome.Stations[RocketSolver.Throat] = originalState;
        var figures = originalFigures;
        figures.CharacteristicVelocity *= 2.0;
        solution.Outcome.Figures[RocketSolver.Throat] = figures;
        Assert.Contains(StationComparison.Compare(c, solution, tolerances), s => s.Contains("characteristicVelocity", StringComparison.Ordinal));
    }

    /// <summary>A failed exit has no stale State, while the earlier stations remain successful.</summary>
    [Fact]
    public void AFailedFrozenExitHasZeroStateAndKeepsEarlierStations()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium")) with
        {
            Flow = FlowModel.FrozenAtChamber,
            Exits = new ExitPlan([300, 1800, 6000, 10518.632521348223],
                [ExitSpecification.AreaRatio, ExitSpecification.AreaRatio, ExitSpecification.AreaRatio, ExitSpecification.AreaRatio]),
        };
        var solution = RocketHost.Solve(CpuFixture.Shared, inputs);
        var failed = solution.StationCount - 1;
        Assert.Equal(CaseStatus.TemperatureOutOfRange, solution.Outcome.StationStatus[failed]);
        Assert.Equal(default, solution.Outcome.Stations[failed]);
        Assert.Equal(default, solution.Outcome.Figures[failed]);
        Assert.All(solution.Outcome.StationStatus.Take(failed), status => Assert.Equal(CaseStatus.Ok, status));
        Assert.True(solution.Outcome.Stations[RocketSolver.Chamber].Temperature > 0.0);
    }
    /// <summary>The cleanup boundary preserves only NoGasPhase's documented T,p.</summary>
    [Fact]
    public void TheNoGasPhaseFinalizerKeepsOnlyTemperatureAndPressure()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        var retained = new MixtureState { Temperature = 300.0, Pressure = 100000.0 };
        var stale = retained;
        stale.Density = 17.0;
        context.Result.Stations[RocketSolver.Chamber] = stale;
        context.Result.Figures[RocketSolver.Chamber] = new PerformanceFigures { AreaRatio = 2.0 };
        context.Result.StationStatus[RocketSolver.Chamber] = (int)CaseStatus.NoGasPhase;
        RocketSolver.FinalizeFailures(in context.Result);
        Assert.Equal(retained, context.Result.Stations[RocketSolver.Chamber]);
        Assert.Equal(default, context.Result.Figures[RocketSolver.Chamber]);
    }

    /// <summary>Every scratch position and the maximum table key participate in exact identity.</summary>
    [Fact]
    public void EveryAllowedCondensedSlotParticipatesInTheIdentity()
    {
        Assert.True(TableLimits.MaxSpecies < 4096, "the twelve-bit packed keys must cover the table limit");
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        var slots = context.Scratch.CondensedInSolution;
        for (var slot = 0; slot < slots.Length; slot++)
        {
            slots[slot] = TableLimits.MaxSpecies - 1 - slot;
        }

        var original = ThroatBracketSearch.CondensedFingerprint(in context);
        for (var slot = 0; slot < slots.Length; slot++)
        {
            var saved = slots[slot];
            slots[slot] = slot;
            Assert.NotEqual(original, ThroatBracketSearch.CondensedFingerprint(in context));
            slots[slot] = saved;
        }

        for (var slot = 0; slot < slots.Length; slot++)
        {
            slots[slot] = TableLimits.MaxSpecies - (int)slots.Length + slot;
        }

        Assert.Equal(original, ThroatBracketSearch.CondensedFingerprint(in context));
    }

}
