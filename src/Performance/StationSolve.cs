using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// One station's solve: the sub-views of the station's row in the case's result, the composition it starts from, and the call
/// into the equilibrium node — the sp solve in shifting flow, the frozen solve when the composition is fixed.
/// </summary>
internal static class StationSolve
{
    /// <summary>The equilibrium (sp) or frozen solve of one station at its pressure, from the composition already in its row; true when Ok.</summary>
    public static bool At(in RocketContext context, in StationRequest request)
    {
        var equilibriumProblem = new EquilibriumProblem(ProblemKind.AssignedEntropyPressure, request.Pressure, request.TemperatureEstimate,
                                                        request.Entropy, context.Problem.ElementMoles);
        var stationResult = ViewsOf(in context, request.Station);
        if (request.Flow == StationFlow.Frozen)
        {
            EquilibriumSolver.SolveFrozen(in context.Table, in equilibriumProblem, in context.Scratch, in stationResult);
        }
        else
        {
            EquilibriumSolver.Solve(in context.Table, in equilibriumProblem, in context.Scratch, in stationResult, true);
        }

        return context.Result.StationStatus[request.Station] == (int)CaseStatus.Ok;
    }

    /// <summary>The station's row of the case's result, as the equilibrium node takes it.</summary>
    public static EquilibriumResult ViewsOf(in RocketContext context, int station)
    {
        var result = context.Result;
        var speciesCount = context.Table.SpeciesCount;
        var elementCount = context.Table.ElementCount;
        return new EquilibriumResult(result.Moles.SubView(station * speciesCount, speciesCount),
                                     result.Multipliers.SubView(station * elementCount, elementCount),
                                     result.Stations.SubView(station, 1),
                                     result.StationStatus.SubView(station, 1),
                                     result.Iterations.SubView(station, 1));
    }

    /// <summary>Copies one station's composition into another's row: the estimate a shifting station starts from, the fixed composition of a frozen one.</summary>
    public static void CopyComposition(in RocketContext context, int from, int to)
    {
        if (from == to)
        {
            return;
        }

        var moles = context.Result.Moles;
        var speciesCount = context.Table.SpeciesCount;
        for (var j = 0; j < speciesCount; j++)
        {
            moles[to * speciesCount + j] = moles[from * speciesCount + j];
        }
    }
}
