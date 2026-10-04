using APThermo.Equilibrium.TraceGas;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: the pieces of the trace-gas pass on the host (TraceGas BOOT.md, <c>## Acceptance criteria</c>, "Host units"): the matrix
/// of a step does not depend on the gaseous moles, the control factor on corrections built by hand, and a failed pass puts the
/// entry's moles and multipliers back bit for bit.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceGasUnitTests
{
    private const double Temperature = 800.0;

    /// <summary>
    /// The system assembled at n and at 1e-12 n of the same multipliers is the same matrix, bit for bit: the element rows are divided by
    /// n and the condensed and total-moles unknowns are relative to it. Only the right-hand side of the element rows carries n.
    /// </summary>
    [Fact]
    public void TheMatrixOfAStepDoesNotDependOnTheGaseousMoles()
    {
        var table = TraceGasCases.TableOver(["MG", "C", "O"]);
        var state = TraceGasCases.MagnesiteBand().First(c => c.Temperature == Temperature).Solve();
        Assert.Equal(CaseStatus.Ok, state.Status);
        using var rig = DerivativeRig.Of(table, Temperature, j => state.Moles[j], [("MgO(cr)", state.Moles[table.IndexOf("MgO(cr)")])]);
        using var elements = CpuFixture.Shared.Accelerator.Allocate1D(state.Case.ElementMoles);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, state.Case.Pressure, Temperature, 0.0, elements.View);
        var view = rig.View;
        for (var i = 0; i < table.ElementCount; i++)
        {
            rig.Result.Multipliers[i] = state.Multipliers[i];
        }

        Composition.EvaluateFunctions(view, rig.Scratch, Temperature);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            SpeciesMarks.Set(rig.Scratch, j, SpeciesMark.Active);
        }

        var layout = TraceGasFrame.LayoutFor(view, problem, 1);
        var logPressure = CaseSetup.LogPressure(problem);
        var matrices = new List<double[]>();
        foreach (var n in new[] { 1.0e-3, 1.0e-15 })
        {
            var sum = TraceGasStep.Fractions(view, rig.Scratch, rig.Result, n, logPressure);
            TraceGasSystem.Assemble(view, problem, rig.Scratch, rig.Result, new TraceGasFrame(layout, logPressure, n, sum, Temperature));
            matrices.Add([.. Enumerable.Range(0, layout.Unknowns * layout.Stride).Select(k => rig.Scratch.Matrix[k])]);
        }

        Assert.Equal(matrices[0].Length, matrices[1].Length);
        Assert.Contains(matrices[0], value => value != 0.0);
        Assert.Equal(matrices[0].Select(BitConverter.DoubleToInt64Bits), matrices[1].Select(BitConverter.DoubleToInt64Bits));
    }

    /// <summary>
    /// The control factor of hand-built corrections over C and O2 (one element each: the change of ln x of C is the correction of C, that of O2 twice the
    /// correction of O): the largest change among the gases above the first retention stage limits the step to 2, and a gas below it
    /// that would grow may not pass ln(1e-4) = −9.2103404.
    /// </summary>
    [Theory]
    [InlineData(3.0, 6.0, -1.0, -19.0, 2.0 / 3.0)]
    [InlineData(1.0, 12.0, -1.0, -19.0, (19.0 - 9.2103404) / 12.0)]
    [InlineData(1.0, -6.0, -1.0, -19.0, 1.0)]
    public void TheControlFactorLimitsTheStepToTwoAndAGrowingTraceToTheSmallSpeciesBound(
        double carbonChange, double oxygenChange, double carbonLogFraction, double oxygenLogFraction, double expected)
    {
        var table = TraceGasCases.TableOver(["C", "O"]);
        var carbon = table.IndexOf("C");
        var oxygen = table.IndexOf("O2");
        using var rig = DerivativeRig.Of(table, Temperature, _ => 0.0, []);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            SpeciesMarks.Set(rig.Scratch, j, j == carbon || j == oxygen ? SpeciesMark.Active : SpeciesMark.Absent);
        }

        var layout = new SystemLayout(ProblemKind.AssignedTemperaturePressure, table.ElementCount, 0, ScratchLayout.MaxUnknowns(table.ElementCount));
        rig.Scratch.RightHandSide[table.Elements.ToList().IndexOf("C")] = carbonChange;
        rig.Scratch.RightHandSide[table.Elements.ToList().IndexOf("O")] = oxygenChange / 2.0;
        rig.Scratch.Corrections[carbon] = carbonLogFraction;
        rig.Scratch.Corrections[oxygen] = oxygenLogFraction;

        var lambda = TraceGasStep.ControlFactor(rig.View, rig.Scratch, layout);

        Assert.Equal(expected, lambda, Tolerances.Exact);
    }

    /// <summary>
    /// A forced failure leaves the entry: an hp case whose enthalpy no state of the window has, run from the entry of a converged state, ends
    /// with a status but <c>Ok</c> and the result's moles and the multipliers of the entry bit for bit (the multipliers are the anchor in
    /// <c>Tie.Elements.Multipliers</c>, where <c>CaseSetup.Begin</c> zeroed the result's).
    /// </summary>
    [Fact]
    public void AFailedPassLeavesTheMolesAndTheMultipliersOfItsEntryBitForBit()
    {
        var entry = TraceGasCases.MagnesiteBand().First(c => c.Temperature == Temperature).Solve();
        Assert.Equal(CaseStatus.Ok, entry.Status);
        var table = entry.Case.Table;
        using var rig = DerivativeRig.Of(table, Temperature, j => entry.Moles[j], []);
        using var elements = CpuFixture.Shared.Accelerator.Allocate1D(entry.Case.ElementMoles);
        var problem = new EquilibriumProblem(ProblemKind.AssignedEnthalpyPressure, entry.Case.Pressure, Temperature, -1.0e12, elements.View);
        var view = rig.View;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            rig.Result.Moles[j] = entry.Moles[j];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            rig.Scratch.Tie.Elements.Multipliers[i] = entry.Multipliers[i];
        }

        var state = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(view, problem, rig.Scratch, rig.Result, EstimateSource.PreviousSolution, ref state));

        var status = TraceGasPass.Run(view, problem, rig.Scratch, rig.Result, CaseSetup.LogPressure(problem), ref state);

        Assert.NotEqual(CaseStatus.Ok, status);
        Assert.True(state.Iterations > 0);
        Assert.Equal(entry.Moles.Select(BitConverter.DoubleToInt64Bits), Enumerable.Range(0, table.SpeciesCount).Select(j => BitConverter.DoubleToInt64Bits(rig.Result.Moles[j])));
        Assert.Equal(entry.Multipliers.Select(BitConverter.DoubleToInt64Bits), Enumerable.Range(0, table.ElementCount).Select(i => BitConverter.DoubleToInt64Bits(rig.Result.Multipliers[i])));
    }
}
