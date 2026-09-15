using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// One exit station assigned by the pressure ratio p_c/p_e (RP-1311 section 6.3.6): the station pressure from the ratio, the
/// solve at that pressure, the velocity, the area ratio and the figures as outputs. No iteration: the pressure is fixed by the
/// ratio, and the area ratio it reaches is reported, not searched for.
/// </summary>
internal static class PressureRatioStation
{
    /// <summary>Solves the station at the pressure the ratio fixes and writes its figures; leaves the station's status otherwise. The caller has already checked that the ratio exceeds 1.</summary>
    public static void At(in RocketContext context, in ChamberReference chamber, in ThroatReference throat,
                          double value, int station, double temperatureEstimate)
    {
        var result = context.Result;
        var flow = context.Problem.Flow == FlowModel.ShiftingEquilibrium ? StationFlow.Shifting : StationFlow.Frozen;
        var pressure = chamber.Pressure / value;
        var request = new StationRequest(station, pressure, temperatureEstimate, chamber.Entropy, flow);
        if (!StationSolve.At(in context, in request))
        {
            return;
        }

        var state = result.Stations[station];
        var velocity = StationFigures.VelocityClamped(chamber.Enthalpy, in state);
        var areaRatio = StationFigures.AreaRatio(throat.MassFlux, in state, velocity);
        StationFigures.Write(in context, station, velocity, areaRatio, value, throat.CharacteristicVelocity);
    }
}
