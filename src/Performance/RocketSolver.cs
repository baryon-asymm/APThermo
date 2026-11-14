using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// Theoretical rocket performance of one case with an infinite-area chamber, RP-1311 Part I chapter 6.
/// The stages own the formulae; this composition root owns station order and failure finalization.
/// </summary>
internal static class RocketSolver
{
    /// <summary>Equation (6.16): the report's relative sonic tolerance.</summary>
    public const double SonicTolerance = 4.0e-5;
    /// <summary>Equation (6.25): the report's correction tolerance for an area-ratio station.</summary>
    public const double AreaRatioTolerance = 4.0e-5;
    /// <summary>Polishing tolerance of the area-ratio iteration.</summary>
    internal const double TightTolerance = 1.0e-10;
    /// <summary>Final relative area residual required by the node invariant.</summary>
    internal const double AreaRatioResidualTolerance = 1.0e-6;
    /// <summary>The owner's throat-decision threshold, followed by the fixed momentum tail.</summary>
    internal const double ThroatDecisionTolerance = 1.0e-8;
    /// <summary>Momentum steps after the throat decision, even beyond the initial iteration cap.</summary>
    internal const int ThroatTailSteps = 2;
    public const int MaxThroatIterations = 20;
    public const int MaxAreaRatioIterations = 20;
    /// <summary>Maximum halvings of the throat bracket.</summary>
    public const int MaxThroatBisections = 60;
    /// <summary>The throat bracket's width floor in ln p.</summary>
    internal const double ThroatBracketWidth = 1.0e-10;
    /// <summary>Reach around gamma=1 where the initial estimate uses its analytic limit.</summary>
    internal const double GammaOneTolerance = 1.0e-6;
    /// <summary>Relative entropy tolerance of every accepted downstream station.</summary>
    internal const double EntropyTolerance = 1.0e-9;
    /// <summary>Area ratio above which the previous station's estimate can be extrapolated.</summary>
    internal const double ExtrapolationAreaRatio = 2.0;
    /// <summary>The two fixed station indices.</summary>
    internal const int Chamber = 0;
    internal const int Throat = 1;

    /// <summary>Solves the case and finalizes every station's status and outward numerical payload.</summary>
    public static void Solve(in SpeciesTableView table, in RocketProblem problem, in EquilibriumScratch scratch, in RocketResult result)
    {
        var stationCount = RocketLayout.StationCount((int)problem.ExitValues.Length);
        result.Status[0] = (int)CaseStatus.InvalidInput;
        for (var station = 0; station < stationCount; station++)
        {
            result.StationStatus[station] = (int)CaseStatus.InvalidInput;
            result.Iterations[station] = 0;
            result.Figures[station] = default;
            result.Stations[station] = default;
        }

        if (!(problem.ChamberPressure > 0.0) || table.SpeciesCount <= 0 || table.ElementCount <= 0 || !IsDefinedFlow(problem.Flow))
        {
            return;
        }

        var context = new RocketContext(in table, in problem, in scratch, in result);
        result.Status[0] = (int)SolveStations(in context);
        FinalizeFailures(in result);
    }

    private static CaseStatus SolveStations(in RocketContext context)
    {
        var chamberStatus = ChamberSolve.At(in context, out var chamber);
        if (chamberStatus != CaseStatus.Ok)
        {
            return chamberStatus;
        }

        var throatStatus = ThroatSearch.At(in context, in chamber, out var throat);
        return throatStatus == CaseStatus.Ok ? ExitStations.All(in context, in chamber, in throat) : throatStatus;
    }

    /// <summary>Keep only the documented NoGasPhase T,p exception and diagnostic composition on failure.</summary>
    internal static void FinalizeFailures(in RocketResult result)
    {
        for (var station = 0; station < result.Stations.Length; station++)
        {
            var status = (CaseStatus)result.StationStatus[station];
            if (status == CaseStatus.Ok)
            {
                continue;
            }

            var failed = result.Stations[station];
            var retained = default(MixtureState);
            if (status == CaseStatus.NoGasPhase)
            {
                retained.Temperature = failed.Temperature;
                retained.Pressure = failed.Pressure;
            }

            result.Stations[station] = retained;
            result.Figures[station] = default;
        }
    }

    private static bool IsDefinedFlow(FlowModel flow) =>
        flow is FlowModel.ShiftingEquilibrium or FlowModel.FrozenAtChamber or FlowModel.FrozenAtThroat;
}