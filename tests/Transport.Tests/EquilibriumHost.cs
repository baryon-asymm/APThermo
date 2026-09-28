using APThermo.Equilibrium;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Transport.Tests;

/// <summary>What one host call of <see cref="Equilibrium.EquilibriumSolver"/> produced: the state this node cross-checks a
/// transport station against, independently of the fixtures and of any typed number.</summary>
internal sealed record EquilibriumEvaluation(CaseStatus Status, double[] Moles, MixtureState State);

/// <summary>
/// The assigned condition of one host call, so <see cref="EquilibriumHost"/>'s private solve step stays within the
/// root's 6-parameter limit (<c>BOOT.md</c>, Constraints, code shape): <c>Temperature</c> is the tp target and
/// <c>Target</c> is the hp enthalpy, one of the two meaningful for the given <see cref="Kind"/>.
/// </summary>
internal readonly record struct EquilibriumCondition(ProblemKind Kind, double Temperature, double Target, double Pressure);

/// <summary>
/// Runs `Equilibrium`'s own kernel-compatible solver on the host, through the `InternalsVisibleTo` grant of 2026-09-28
/// (the orchestrator's decision under <c>AGENTS.md</c> §11, recorded in <c>src/Equilibrium/API.md</c> and this node's
/// <c>BOOT.md</c>, `## Dependencies`). Every case is solved cold (<c>useMolesAsEstimate: false</c>): these facts need one
/// independent state each, not a warm-started sequence.
/// </summary>
internal static class EquilibriumHost
{
    /// <summary>Solves a tp problem (<see cref="ProblemKind.AssignedTemperaturePressure"/>).</summary>
    public static EquilibriumEvaluation SolveTp(Accelerator accelerator, SpeciesTable table, double[] elementMoles, double temperature, double pressure) =>
        Solve(accelerator, table, elementMoles, new EquilibriumCondition(ProblemKind.AssignedTemperaturePressure, temperature, Target: 0.0, pressure));

    /// <summary>Solves an hp problem (<see cref="ProblemKind.AssignedEnthalpyPressure"/>) from the default 3 800 K estimate.</summary>
    public static EquilibriumEvaluation SolveHp(Accelerator accelerator, SpeciesTable table, double[] elementMoles, double enthalpy, double pressure) =>
        Solve(accelerator, table, elementMoles, new EquilibriumCondition(ProblemKind.AssignedEnthalpyPressure, Temperature: 0.0, enthalpy, pressure));

    /// <summary>Uploads one case's element moles and scratch, runs the solver, and reads the result back.</summary>
    private static EquilibriumEvaluation Solve(Accelerator accelerator, SpeciesTable table, double[] elementMoles, EquilibriumCondition condition)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var speciesBuffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var elementMolesBuffer = accelerator.Allocate1D(elementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var moles = accelerator.Allocate1D<double>(speciesCount);
        using var multipliers = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);
        var problem = new EquilibriumProblem(condition.Kind, condition.Pressure, condition.Temperature, condition.Target, elementMolesBuffer.View);
        var speciesView = speciesBuffers.View;
        EquilibriumSolver.Solve(in speciesView, in problem, in scratch, in result, useMolesAsEstimate: false);
        var solvedStatus = (CaseStatus)status.GetAsArray1D()[0];
        return new EquilibriumEvaluation(solvedStatus, moles.GetAsArray1D(), state.GetAsArray1D()[0]);
    }
}
