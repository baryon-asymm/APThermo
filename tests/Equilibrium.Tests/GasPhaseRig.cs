using APThermo.Equilibrium.GasPhase;
using APThermo.Equilibrium.Recovery;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>What one call of <see cref="GasPhaseVerdict.Decide"/> did to the views it was given.</summary>
internal sealed record VerdictRun(GasVerdict Verdict, CondensedFigures Figures, Iterate Before, Iterate After, MixtureState StateAfter);

/// <summary>The moles and the multipliers a failed attempt leaves.</summary>
internal sealed record Iterate(double[] Moles, double[] Multipliers);

/// <summary>Calls the gas phase's verdict directly on the host over CPU-accelerator buffers, with the moles and multipliers of a failed attempt given.</summary>
internal static class GasPhaseRig
{
    /// <summary>The verdict for a tp case, the views holding <paramref name="moles"/> and <paramref name="multipliers"/> on entry.</summary>
    public static VerdictRun Decide(EquilibriumCase problem, double[] moles, double[] multipliers)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var molesBuffer = accelerator.Allocate1D(moles);
        using var multipliersBuffer = accelerator.Allocate1D(multipliers);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        state.MemSetToZero();
        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(molesBuffer.View, multipliersBuffer.View, state.View, status.View, iterations.View);
        var view = buffers.View;

        // The marks and the element mask are what an attempt's Begin leaves; the verdict is asked after one.
        var attempt = new IterationState();
        var begin = CaseSetup.Begin(view, input, scratch, result, EstimateSource.Defaults, ref attempt);
        Assert.Equal(CaseStatus.Ok, begin);
        molesBuffer.CopyFromCPU(moles);
        multipliersBuffer.CopyFromCPU(multipliers);

        var verdict = GasPhaseVerdict.Decide(in view, in input, in scratch, in result, out var figures);
        return new VerdictRun(
            verdict, figures, new Iterate((double[])moles.Clone(), (double[])multipliers.Clone()),
            new Iterate(molesBuffer.GetAsArray1D(), multipliersBuffer.GetAsArray1D()), state.GetAsArray1D()[0]);
    }

    /// <summary>The dead-end floor <see cref="DeadEnds.FloorBelow"/> gives for <paramref name="temperature"/> over the marks that a case's <c>Begin</c> leaves.</summary>
    public static double FloorBelow(EquilibriumCase problem, double temperature)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var molesBuffer = accelerator.Allocate1D<double>(speciesCount);
        using var multipliersBuffer = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        molesBuffer.MemSetToZero();
        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(molesBuffer.View, multipliersBuffer.View, state.View, status.View, iterations.View);
        var attempt = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(buffers.View, input, scratch, result, EstimateSource.Defaults, ref attempt));
        return DeadEnds.FloorBelow(buffers.View, scratch, temperature);
    }

    /// <summary>
    /// The Newton steps of the first cold attempt of a case alone: <see cref="CaseSetup.Begin"/> and
    /// <see cref="ConvergenceSequence.Run"/> over fresh views, none of the attempts that follow a failure.
    /// </summary>
    public static int FirstAttemptIterations(EquilibriumCase problem)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var molesBuffer = accelerator.Allocate1D<double>(speciesCount);
        using var multipliersBuffer = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        molesBuffer.MemSetToZero();
        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(molesBuffer.View, multipliersBuffer.View, state.View, status.View, iterations.View);
        var view = buffers.View;
        var attempt = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(view, input, scratch, result, EstimateSource.Defaults, ref attempt));
        _ = ConvergenceSequence.Run(view, input, scratch, result, CaseSetup.LogPressure(input), ref attempt);
        return attempt.Iterations;
    }

    /// <summary>
    /// The vertex the simplex ends at, before the tangent-plane search: the number of directions of the face of optimal
    /// multipliers and the multipliers π0 of the vertex itself, by element (zero for an absent element), with the moles of the
    /// condensed minimum. Null when the program does not reach an optimal basis.
    /// </summary>
    public static Vertex? VertexOf(EquilibriumCase problem)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var molesBuffer = accelerator.Allocate1D<double>(speciesCount);
        using var multipliersBuffer = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(molesBuffer.View, multipliersBuffer.View, state.View, status.View, iterations.View);
        var view = buffers.View;
        var attempt = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(view, input, scratch, result, EstimateSource.Defaults, ref attempt));
        Composition.EvaluateFunctions(view, scratch, problem.Temperature);
        var rows = CondensedSimplex.Minimize(view, input, scratch, problem.Temperature, out _);
        if (rows == 0)
        {
            return null;
        }

        CondensedSimplex.WriteMoles(view, scratch, result, rows);
        var directions = TangentPlane.Directions(view, input, scratch, rows);
        var pi = new double[elementCount];
        for (var r = 0; r < rows; r++)
        {
            pi[scratch.Tie.CondensedSet[r]] = scratch.Tie.LogMoles[r];
        }

        return new Vertex(directions, pi, molesBuffer.GetAsArray1D());
    }

    /// <summary>
    /// The optimal basis of the program with every gas a column at unit fraction (TraceGas BOOT.md, "The gas basis"): each basic
    /// species by name with its amount (kmol/kg, zero for a basic at zero level). Null when the program does not complete.
    /// </summary>
    public static Dictionary<string, double>? GasBasisOf(EquilibriumCase problem)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var molesBuffer = accelerator.Allocate1D<double>(speciesCount);
        using var multipliersBuffer = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(molesBuffer.View, multipliersBuffer.View, state.View, status.View, iterations.View);
        var view = buffers.View;
        var attempt = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(view, input, scratch, result, EstimateSource.Defaults, ref attempt));
        if (!GasPhaseVerdict.PhaseOnePoint(view, input, scratch, result, true, out _))
        {
            return null;
        }

        var basis = new Dictionary<string, double>();
        var rows = Enumerable.Range(0, elementCount).Count(i => problem.ElementMoles[i] > 0.0);
        for (var k = 0; k < rows; k++)
        {
            var column = scratch.CondensedInSolution[k];
            basis[column >= 0 ? table.Species[column] : "artificial"] = scratch.Corrections[k];
        }

        return basis;
    }
}

/// <summary>The condensed minimum before the tangent-plane search: the face's dimension, the vertex multipliers and the moles.</summary>
internal sealed record Vertex(int Directions, double[] Multipliers, double[] Moles);
