using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// Theoretical rocket performance of one case with an infinite-area chamber, RP-1311 Part I chapter 6: the chamber at
/// assigned enthalpy and pressure, the throat by the sonic condition (equations 6.15 to 6.17), the exit stations at assigned
/// pressure ratios or area ratios (6.21 to 6.25), in shifting equilibrium or with the composition frozen at the chamber or
/// at the throat (6.5). Kernel-compatible: static, no allocation, no exceptions.
/// </summary>
/// <remarks>
/// This is the contract and the order of the stations; every formula lives in the stage it belongs to — <see cref="ChamberSolve"/>,
/// <see cref="ThroatSearch"/>, <see cref="ExitStations"/>, <see cref="AreaRatioIteration"/>, <see cref="StationSolve"/> and
/// <see cref="StationFigures"/> — as the node's BOOT.md sets out under Structure.
/// </remarks>
public static class RocketSolver
{
    /// <summary>Equation (6.16): the throat is sonic when |u² − a²|/u² is within this.</summary>
    public const double SonicTolerance = 4.0e-5;

    /// <summary>Equation (6.25): an assigned area ratio is met when the last correction of ln(p_c/p_e) is within this.</summary>
    public const double AreaRatioTolerance = 4.0e-5;

    /// <summary>Iterations continue past the report's tolerances until the correction is this small, so that the reported station is at rounding level.</summary>
    internal const double TightTolerance = 1.0e-10;

    public const int MaxThroatIterations = 20;
    public const int MaxAreaRatioIterations = 20;

    /// <summary>Above this area ratio the report's analytic extrapolation from the previous station gives the initial estimate.</summary>
    internal const double ExtrapolationAreaRatio = 2.0;

    /// <summary>The station indices of the two fixed stations; the k-th exit is <see cref="RocketLayout.FixedStations"/> + k.</summary>
    internal const int Chamber = 0;
    internal const int Throat = 1;

    /// <summary>Solves the case; every station's state, composition, figures and status are written, then the case status.</summary>
    public static void Solve(in SpeciesTableView table, in RocketProblem problem, in EquilibriumScratch scratch, in RocketResult result)
    {
        var exitCount = (int)problem.ExitValues.Length;
        var stationCount = RocketLayout.StationCount(exitCount);
        result.Status[0] = (int)CaseStatus.InvalidInput;
        for (var station = 0; station < stationCount; station++)
        {
            result.StationStatus[station] = (int)CaseStatus.InvalidInput;
            result.Iterations[station] = 0;
            result.Figures[station] = default;
        }

        if (!(problem.ChamberPressure > 0.0) || table.SpeciesCount <= 0 || table.ElementCount <= 0)
        {
            return;
        }

        var context = new RocketContext(in table, in problem, in scratch, in result);
        var chamberStatus = ChamberSolve.At(in context, out var chamber);
        if (chamberStatus != CaseStatus.Ok)
        {
            result.Status[0] = (int)chamberStatus;
            return;
        }

        var throatStatus = ThroatSearch.At(in context, in chamber, out var throat);
        if (throatStatus != CaseStatus.Ok)
        {
            result.Status[0] = (int)throatStatus;
            return;
        }

        result.Status[0] = (int)ExitStations.All(in context, in chamber, in throat);
    }
}
