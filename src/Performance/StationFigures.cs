using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// The quantities RP-1311 section 6.2 defines at a station: the velocity from the energy equation, the area ratio from the
/// throat's mass flux, and the figures of the throat and of every exit, each formula here once. The characteristic velocity is the
/// throat's; <see cref="ThroatSearch"/> writes it, with p_c/p = 1, into the chamber's figures.
/// </summary>
internal static class StationFigures
{
    /// <summary>The energy equation of section 6.2: u² = 2(h_c − h).</summary>
    public static double VelocitySquared(double chamberEnthalpy, in MixtureState state) => 2.0 * (chamberEnthalpy - state.Enthalpy);

    /// <summary>The velocity of the energy equation; NaN where the station lies above the chamber enthalpy.</summary>
    public static double Velocity(double chamberEnthalpy, in MixtureState state) => Math.Sqrt(VelocitySquared(chamberEnthalpy, in state));

    /// <summary>
    /// The velocity of the energy equation with the radicand clamped at zero, as the pressure-ratio station has always computed
    /// it. The two variants are kept apart verbatim: whether a negative radicand is a zero velocity or a NotConverged station is
    /// one rule with the never-supersonic outcome and is decided in its own session (BOOT.md, the Structure of 2026-09-14).
    /// </summary>
    public static double VelocityClamped(double chamberEnthalpy, in MixtureState state) =>
        Math.Sqrt(Math.Max(VelocitySquared(chamberEnthalpy, in state), 0.0));

    /// <summary>The area ratio A/A_t from the throat's mass flux and the station's: (ρ_t u_t)/(ρ u).</summary>
    public static double AreaRatio(double throatMassFlux, in MixtureState state, double velocity) => throatMassFlux / (state.Density * velocity);

    /// <summary>Writes the velocity and Mach number into the station's state and its performance figures (6.2).</summary>
    public static void Write(in RocketContext context, int station, double velocity, double areaRatio, double pressureRatio,
                             double characteristicVelocity)
    {
        var result = context.Result;
        var state = result.Stations[station];
        state.Velocity = velocity;
        state.Mach = velocity / state.SoundSpeed;
        result.Stations[station] = state;
        var figures = result.Figures[station];
        figures.AreaRatio = areaRatio;
        figures.PressureRatio = pressureRatio;
        figures.CharacteristicVelocity = characteristicVelocity;
        figures.ThrustCoefficient = velocity / characteristicVelocity;
        figures.SpecificImpulse = velocity;
        figures.VacuumSpecificImpulse = velocity + state.Pressure / (state.Density * velocity);
        result.Figures[station] = figures;
    }
}
