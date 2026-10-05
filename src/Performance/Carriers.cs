using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// The four views one rocket case is solved over, built once in <see cref="RocketSolver.Solve"/> and passed to every stage:
/// the species table, the case, its scratch and the result it writes into. Blittable, as the kernel requires.
/// </summary>
internal readonly struct RocketContext(in SpeciesTableView table, in RocketProblem problem, in EquilibriumScratch scratch, in RocketResult result)
{
    public readonly SpeciesTableView Table = table;
    public readonly RocketProblem Problem = problem;
    public readonly EquilibriumScratch Scratch = scratch;
    public readonly RocketResult Result = result;
}

/// <summary>What the chamber fixes for every station downstream of it: its pressure, enthalpy, entropy and isentropic exponent.</summary>
internal readonly struct ChamberReference(double pressure, double enthalpy, double entropy, double gammaS)
{
    public readonly double Pressure = pressure;
    public readonly double Enthalpy = enthalpy;
    public readonly double Entropy = entropy;
    public readonly double GammaS = gammaS;
}

/// <summary>What the throat fixes for the exit stations: its pressure and isentropic exponent, the mass flux, the characteristic velocity and ln(p_c/p_t).</summary>
internal readonly struct ThroatReference(double pressure, double massFlux, double characteristicVelocity, double logPressureRatio, double gammaS)
{
    public readonly double Pressure = pressure;
    public readonly double MassFlux = massFlux;
    public readonly double CharacteristicVelocity = characteristicVelocity;
    public readonly double LogPressureRatio = logPressureRatio;
    public readonly double GammaS = gammaS;
}

/// <summary>What the estimate of the next exit station is made of: the temperature of the last station that converged and the extrapolation state of (6.23).</summary>
internal struct ExitEstimate
{
    /// <summary>K, the temperature the next station's solve starts from.</summary>
    public double Temperature;

    /// <summary>Whether the previous station may be extrapolated from: it converged and its area ratio exceeds <see cref="RocketSolver.ExtrapolationAreaRatio"/>.</summary>
    public bool Extrapolable;

    /// <summary>ln(p_c/p_e) of the last exit accepted by area ratio; read only while <see cref="Extrapolable"/>.</summary>
    public double LogPressureRatio;

    /// <summary>ln(A/A_t) of that exit, meaningful only while <see cref="Extrapolable"/>.</summary>
    public double LogAreaRatio;

    /// <summary>∂ln(A/A_t)/∂ln(p_c/p_e) at constant entropy at that exit, equation (6.23), meaningful only while <see cref="Extrapolable"/>.</summary>
    public double Derivative;
}

/// <summary>How the area-ratio iteration of one exit station ended.</summary>
internal enum ExitOutcome
{
    /// <summary>The correction of (6.25) fell below the tight tolerance.</summary>
    Converged,

    /// <summary>The iterations ran out; the last correction is within the report's tolerance, and the station is accepted.</summary>
    WithinReportTolerance,

    /// <summary>The iterations ran out and the last correction is above the report's tolerance.</summary>
    NotMet,

    /// <summary>Every pass fell on the subsonic side of the sonic point, so no correction was ever computed.</summary>
    NeverSupersonic,

    /// <summary>A station solve did not return Ok; the station carries the equilibrium node's status.</summary>
    SolveFailed,
}

/// <summary>Which solver a station is solved with: the composition follows the equilibrium, or it stays the one already in the station's row.</summary>
internal enum StationFlow
{
    Shifting,
    Frozen,
}

/// <summary>One station's solve as it is asked for: where it is written, at which pressure, from which temperature estimate, at which entropy, in which flow.</summary>
internal readonly struct StationRequest(int station, double pressure, double temperatureEstimate, double entropy, StationFlow flow)
{
    public readonly int Station = station;
    public readonly double Pressure = pressure;
    public readonly double TemperatureEstimate = temperatureEstimate;
    public readonly double Entropy = entropy;
    public readonly StationFlow Flow = flow;
}

/// <summary>
/// What <see cref="StationFigures.Write"/> writes into one station: its velocity (also the source of its Mach number)
/// and the three figures a station's own stage has just computed. Grouped so that <c>Write</c>, which also takes the
/// chamber's entropy for the isentropic-expansion check (BOOT.md, Invariants, 2026-09-26), stays within the root's
/// parameter limit.
/// </summary>
internal readonly struct StationFigureInputs(double velocity, double areaRatio, double pressureRatio, double characteristicVelocity)
{
    public readonly double Velocity = velocity;
    public readonly double AreaRatio = areaRatio;
    public readonly double PressureRatio = pressureRatio;
    public readonly double CharacteristicVelocity = characteristicVelocity;
}

/// <summary>
/// What every stage of the throat search (<see cref="ThroatBracketSearch"/>) shares: the case, the chamber it
/// expands from, and the flow model the throat's own station is solved with. Grouped so that the search's methods
/// stay within the root's parameter limit, as <see cref="StationFigureInputs"/> does for <see cref="StationFigures.Write"/>.
/// </summary>
internal readonly struct ThroatQuery(in RocketContext context, in ChamberReference chamber)
{
    public readonly RocketContext Context = context;
    public readonly ChamberReference Chamber = chamber;
    public readonly StationFlow Flow = context.Problem.Flow == FlowModel.FrozenAtChamber ? StationFlow.Frozen : StationFlow.Shifting;
}

/// <summary>
/// The throat's bracket (BOOT.md, "the throat is the first maximum of the mass flux met from the chamber"): the smallest pressure solved with
/// u²/a² &lt; 1 (the subsonic end, closest to the chamber) and the largest solved with u²/a² &gt; 1 (the supersonic
/// end), each carrying the temperature and the condensed-species fingerprint of the state solved there. Tracking
/// keeps whichever end is closer to the sonic point, so the same rule serves the momentum trials and the
/// bisection that narrows the bracket between them.
/// </summary>
internal struct ThroatBracket
{
    public bool HasSubsonic;
    public double SubsonicPressure;
    public double SubsonicTemperature;
    public long SubsonicFingerprint;

    public bool HasSupersonic;
    public double SupersonicPressure;
    public double SupersonicTemperature;
    public long SupersonicFingerprint;

    /// <summary>Both ends of the bracket have been seen.</summary>
    public readonly bool IsComplete => HasSubsonic && HasSupersonic;

    /// <summary>The bracket's width in ln p; meaningful only while <see cref="IsComplete"/>.</summary>
    public readonly double LogWidth => KernelMath.Log(SubsonicPressure) - KernelMath.Log(SupersonicPressure);

    /// <summary>Records one trial if it narrows its side of the bracket (the smallest subsonic pressure, the largest supersonic one).</summary>
    public void Track(double pressure, double temperature, double sonicRatio, long fingerprint)
    {
        if (sonicRatio < 1.0)
        {
            if (!HasSubsonic || pressure < SubsonicPressure)
            {
                HasSubsonic = true;
                SubsonicPressure = pressure;
                SubsonicTemperature = temperature;
                SubsonicFingerprint = fingerprint;
            }
        }
        else if (sonicRatio > 1.0)
        {
            if (!HasSupersonic || pressure > SupersonicPressure)
            {
                HasSupersonic = true;
                SupersonicPressure = pressure;
                SupersonicTemperature = temperature;
                SupersonicFingerprint = fingerprint;
            }
        }
    }
}

/// <summary>
/// One end of a bracket <see cref="PhaseBoundaryLocator"/> narrows: a solved point's pressure, its temperature (the
/// next solve's estimate), its sonic ratio u²/a² (0 where u² was not positive, the same subsonic reading
/// <see cref="ThroatBracketSearch"/>'s trials use, BOOT.md, 2026-09-28) and the condensed-species fingerprint of the
/// state solved there.
/// </summary>
internal readonly struct PhaseBoundaryEnd(double pressure, double temperature, double sonicRatio, long fingerprint)
{
    public readonly double Pressure = pressure;
    public readonly double Temperature = temperature;
    public readonly double SonicRatio = sonicRatio;
    public readonly long Fingerprint = fingerprint;
}

/// <summary>
/// What <see cref="PhaseBoundaryLocator.Locate"/> narrows between: the outer end (closer to the chamber, whose
/// fingerprint the search holds fixed while it narrows) and the inner end (closer to the candidate throat).
/// </summary>
internal readonly struct PhaseBoundaryQuery(in PhaseBoundaryEnd outer, in PhaseBoundaryEnd inner)
{
    public readonly PhaseBoundaryEnd Outer = outer;
    public readonly PhaseBoundaryEnd Inner = inner;
}

/// <summary>
/// One condensed-set boundary along the chamber isentrope (BOOT.md, 2026-09-28, <c>PhaseBoundaryLocator</c>): the
/// last point on the outer (chamber) side still carrying the outer fingerprint, and the first point on the inner
/// (candidate) side that no longer does. <see cref="UpstreamChokeCheck"/> reads both ends' sonic ratios to decide
/// whether an earlier choke lies above, at, or below this boundary.
/// </summary>
internal struct PhaseBoundary
{
    public PhaseBoundaryEnd Hi;
    public PhaseBoundaryEnd Lo;
}
