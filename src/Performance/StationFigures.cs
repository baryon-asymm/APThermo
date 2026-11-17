using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// The quantities RP-1311 section 6.2 defines at a station: the velocity from the energy equation, the area ratio from the
/// throat's mass flux, and the figures of the throat and of every exit, each formula here once. This stage owns writes to
/// station payloads, including their initial state and the documented failed-station finalization policy.
/// </summary>
internal static class StationFigures
{
    /// <summary>Initialize the requested station rows before any solve.</summary>
    internal static void Initialize(in RocketResult result, int stationCount)
    {
        for (var station = 0; station < stationCount; station++)
        {
            result.StationStatus[station] = (int)CaseStatus.InvalidInput;
            result.Iterations[station] = 0;
            result.Figures[station] = default;
            result.Stations[station] = default;
        }
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

    /// <summary>The accepted throat's mass flux, characteristic velocity and figures, and the chamber's figures.</summary>
    internal static ThroatReference Throat(in RocketContext context, in ChamberReference chamber, double pressureThroat)
    {
        var result = context.Result;
        var pressureChamber = chamber.Pressure;
        var throatState = result.Stations[RocketSolver.Throat];
        var velocityThroat = Velocity(chamber.Enthalpy, in throatState);
        var massFluxThroat = throatState.Density * velocityThroat;
        var characteristicVelocity = pressureChamber / massFluxThroat;
        var inputs = new StationFigureInputs(velocityThroat, 1.0, pressureChamber / pressureThroat, characteristicVelocity);
        Write(in context, RocketSolver.Throat, in inputs, chamber.Entropy);
        var chamberFigures = result.Figures[RocketSolver.Chamber];
        chamberFigures.PressureRatio = 1.0;
        chamberFigures.CharacteristicVelocity = characteristicVelocity;
        result.Figures[RocketSolver.Chamber] = chamberFigures;
        return new ThroatReference(pressureThroat, massFluxThroat, characteristicVelocity, KernelMath.Log(pressureChamber / pressureThroat),
                                   throatState.GammaS);
    }

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
        Math.Sqrt(KernelMath.Max(VelocitySquared(chamberEnthalpy, in state), 0.0));

    /// <summary>The natural logarithm of a ratio the exit iteration works in: ln(A/A_t) and ln(p_c/p).</summary>
    public static double LogOf(double ratio) => KernelMath.Log(ratio);

    /// <summary>The pressure <c>p_c exp(−ln(p_c/p))</c> of the station whose logarithmic pressure ratio is <paramref name="logPressureRatio"/>.</summary>
    public static double PressureAt(double chamberPressure, double logPressureRatio) => chamberPressure * KernelMath.Exp(-logPressureRatio);

    /// <summary>The area ratio A/A_t from the throat's mass flux and the station's: (ρ_t u_t)/(ρ u).</summary>
    public static double AreaRatio(double throatMassFlux, in MixtureState state, double velocity) => throatMassFlux / (state.Density * velocity);

    /// <summary>
    /// Writes the velocity and Mach number into the station's state and its performance figures (6.2), then checks
    /// the isentropic-expansion invariant (BOOT.md, Invariants, 2026-09-26): a station whose entropy departs from the
    /// chamber's by more than <see cref="RocketSolver.EntropyTolerance"/> relative is reported as NotConverged instead.
    /// </summary>
    public static void Write(in RocketContext context, int station, in StationFigureInputs inputs, double chamberEntropy)
    {
        var result = context.Result;
        var state = result.Stations[station];
        var velocity = inputs.Velocity;
        state.Velocity = velocity;
        state.Mach = velocity / state.SoundSpeed;
        result.Stations[station] = state;
        var figures = result.Figures[station];
        figures.AreaRatio = inputs.AreaRatio;
        figures.PressureRatio = inputs.PressureRatio;
        figures.CharacteristicVelocity = inputs.CharacteristicVelocity;
        figures.ThrustCoefficient = velocity / inputs.CharacteristicVelocity;
        figures.SpecificImpulse = velocity;
        figures.VacuumSpecificImpulse = velocity + state.Pressure / (state.Density * velocity);
        result.Figures[station] = figures;
        if (!Finite(velocity) || !Finite(state.Mach) || !Finite(figures.AreaRatio) || !Finite(figures.PressureRatio)
            || !Finite(figures.CharacteristicVelocity) || !Finite(figures.ThrustCoefficient)
            || !Finite(figures.SpecificImpulse) || !Finite(figures.VacuumSpecificImpulse)
            || !(Math.Abs(state.Entropy - chamberEntropy) <= RocketSolver.EntropyTolerance * Math.Abs(chamberEntropy)))
        {
            result.StationStatus[station] = (int)CaseStatus.NotConverged;
        }
    }

    private static bool Finite(double value) => Math.Abs(value) < double.PositiveInfinity;
}
