using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// The bracket-and-bisection half of the throat search (BOOT.md, "the throat is the first maximum of the mass flux
/// met from the chamber", finding F1): the momentum iterations of equations (6.15) to (6.17), which also track the
/// smallest-subsonic / largest-supersonic bracket along the way, and, where the 20 iterations end without the sonic
/// point, a bisection of that bracket in ln p, or, failing that, the plateau-edge acceptance of BOOT.md. Returns the
/// pressure the throat is finally solved at; <see cref="ThroatSearch"/> turns that state into the case's figures.
/// </summary>
internal static class ThroatBracketSearch
{
    /// <summary>Finds the throat pressure: the momentum iterations, then the bracket's bisection if they end short.</summary>
    public static CaseStatus Locate(in ThroatQuery query, double temperatureEstimate, out double pressureSolved)
    {
        var bracket = default(ThroatBracket);
        var status = Momentum(in query, temperatureEstimate, ref bracket, out pressureSolved, out var sonicRatio, out var converged);
        return status != CaseStatus.Ok
            ? status
            : !converged && !(Math.Abs(sonicRatio - 1.0) <= RocketSolver.SonicTolerance)
                ? Resolve(in query, ref bracket, ref pressureSolved)
                : CaseStatus.Ok;
    }

    /// <summary>How many bracket widths (<see cref="RocketSolver.ThroatBracketWidth"/>) the plateau-edge acceptance
    /// steps back toward the chamber when a re-solve lands on the far side of the edge (BOOT.md, 2026-09-28, finding F3).</summary>
    private const int MaxEdgeRetries = 8;

    /// <summary>The momentum iterations of (6.15) to (6.17); tracks the bracket of BOOT.md along the way. The
    /// pressure of the state actually solved is kept apart from the next candidate, so a natural exhaustion of the
    /// loop reports the state it holds, not a pressure one momentum step past it (BOOT.md, finding F6).</summary>
    private static CaseStatus Momentum(in ThroatQuery query, double temperatureEstimate, ref ThroatBracket bracket,
                                       out double pressureSolved, out double sonicRatio, out bool converged)
    {
        var context = query.Context;
        var result = context.Result;
        var chamber = query.Chamber;
        var gammaChamber = chamber.GammaS;

        // Equation (6.15): the first estimate of the throat pressure from the chamber's isentropic exponent, through
        // its limit at gamma_s = 1 exactly, where (6.15) itself is Math.Pow(1, +-infinity) = 1, the chamber pressure
        // (BOOT.md, 2026-09-28, finding F4): the equilibrium node's plateau convention for an undissociated gas.
        var pressureCandidate = Math.Abs(gammaChamber - 1.0) <= RocketSolver.GammaOneTolerance
            ? chamber.Pressure * Math.Exp(-0.5)
            : chamber.Pressure / Math.Pow(0.5 * (gammaChamber + 1.0), gammaChamber / (gammaChamber - 1.0));
        pressureSolved = pressureCandidate;
        sonicRatio = 0.0;
        converged = false;
        for (var k = 0; k < RocketSolver.MaxThroatIterations; k++)
        {
            pressureSolved = pressureCandidate;
            var request = new StationRequest(RocketSolver.Throat, pressureSolved, temperatureEstimate, chamber.Entropy, query.Flow);
            if (!StationSolve.At(in context, in request))
            {
                return (CaseStatus)result.StationStatus[RocketSolver.Throat];
            }

            var state = result.Stations[RocketSolver.Throat];
            if (!TryRatio(chamber.Enthalpy, in state, out var ratio))
            {
                break;
            }

            // The ratio is always computed and kept before any test below, so a break never leaves it stale
            // (BOOT.md, 2026-09-28, observation O2).
            sonicRatio = ratio;
            bracket.Track(pressureSolved, state.Temperature, ratio, CondensedFingerprint(in context));
            if (Math.Abs(ratio - 1.0) <= RocketSolver.TightTolerance)
            {
                converged = true;
                break;
            }

            // Equation (6.17): the momentum relation from the current estimate to the sonic point. The division is
            // grouped before the multiplication, as the original `pressureThroat *= a / b` compiled it, so a
            // converging case's bits are unchanged (BOOT.md, Structure: every expression keeps its present order).
            // A non-positive u² (ratio 0, BOOT.md finding F4) steps the pressure down by the same formula, instead
            // of ending the search without a bracket.
            pressureCandidate = pressureSolved * ((1.0 + state.GammaS * ratio) / (1.0 + state.GammaS));
            temperatureEstimate = state.Temperature;
        }

        return CaseStatus.Ok;
    }

    /// <summary>Bisects the bracket to the sonic point, or, failing that, accepts the plateau edge (BOOT.md).</summary>
    private static CaseStatus Resolve(in ThroatQuery query, ref ThroatBracket bracket, ref double pressureSolved)
    {
        if (!bracket.IsComplete)
        {
            return CaseStatus.ThroatNotFound;
        }

        var status = Bisect(in query, ref bracket, out var bisected);
        if (status != CaseStatus.Ok)
        {
            return status != CaseStatus.ThroatNotFound || bracket.SubsonicFingerprint == bracket.SupersonicFingerprint
                ? status
                : AcceptPlateauEdge(in query, in bracket, ref pressureSolved);
        }

        pressureSolved = bisected;
        return CaseStatus.Ok;
    }

    /// <summary>Halves the bracket in ln p, at most <see cref="RocketSolver.MaxThroatBisections"/> times; the last
    /// trial is tested against the report's own tolerance before giving up (BOOT.md, 2026-09-28, observation O1).</summary>
    internal static CaseStatus Bisect(in ThroatQuery query, ref ThroatBracket bracket, out double pressureSolved)
    {
        var context = query.Context;
        var chamber = query.Chamber;
        pressureSolved = bracket.SubsonicPressure;
        var lastRatio = double.NaN;
        for (var b = 0; b < RocketSolver.MaxThroatBisections; b++)
        {
            if (bracket.LogWidth < RocketSolver.ThroatBracketWidth)
            {
                break;
            }

            var midPressure = Math.Exp(0.5 * (Math.Log(bracket.SubsonicPressure) + Math.Log(bracket.SupersonicPressure)));
            var midTemperature = 0.5 * (bracket.SubsonicTemperature + bracket.SupersonicTemperature);
            var request = new StationRequest(RocketSolver.Throat, midPressure, midTemperature, chamber.Entropy, query.Flow);
            if (!StationSolve.At(in context, in request))
            {
                return (CaseStatus)context.Result.StationStatus[RocketSolver.Throat];
            }

            var state = context.Result.Stations[RocketSolver.Throat];
            if (!TryRatio(chamber.Enthalpy, in state, out var ratio))
            {
                return CaseStatus.ThroatNotFound;
            }

            bracket.Track(midPressure, state.Temperature, ratio, CondensedFingerprint(in context));
            lastRatio = ratio;
            pressureSolved = midPressure;
            if (Math.Abs(ratio - 1.0) <= RocketSolver.TightTolerance)
            {
                return CaseStatus.Ok;
            }
        }

        return Math.Abs(lastRatio - 1.0) <= RocketSolver.SonicTolerance ? CaseStatus.Ok : CaseStatus.ThroatNotFound;
    }

    /// <summary>
    /// Accepts the plateau edge: the single-phase state on the chamber side of the edge, re-solved so the buffers
    /// hold it. Seeded from the chamber's own (single-phase) composition, not whatever the bisection's last trial
    /// left in the throat's row, which may sit on the pinned-pair side of the edge (found while writing this
    /// criterion's evidence: an unseeded re-solve here converged to the pinned pair even at a genuinely single-phase
    /// pressure). The re-solved state is accepted only when it lands back on the bracket's own subsonic side (BOOT.md,
    /// 2026-09-28, finding F3); otherwise the search steps a few bracket widths further toward the chamber, up to
    /// <see cref="MaxEdgeRetries"/> times, where ρu moves by about the bracket width, relative.
    /// </summary>
    internal static CaseStatus AcceptPlateauEdge(in ThroatQuery query, in ThroatBracket bracket, ref double pressureSolved)
    {
        var context = query.Context;
        var chamber = query.Chamber;
        for (var attempt = 0; attempt <= MaxEdgeRetries; attempt++)
        {
            var pressure = bracket.SubsonicPressure * Math.Exp(attempt * RocketSolver.ThroatBracketWidth);
            StationSolve.CopyComposition(in context, RocketSolver.Chamber, RocketSolver.Throat);
            var request = new StationRequest(RocketSolver.Throat, pressure, bracket.SubsonicTemperature, chamber.Entropy, query.Flow);
            if (!StationSolve.At(in context, in request))
            {
                return (CaseStatus)context.Result.StationStatus[RocketSolver.Throat];
            }

            var state = context.Result.Stations[RocketSolver.Throat];
            if (CondensedFingerprint(in context) == bracket.SubsonicFingerprint && TryRatio(chamber.Enthalpy, in state, out var ratio) && ratio < 1.0)
            {
                pressureSolved = pressure;
                return CaseStatus.Ok;
            }
        }

        return CaseStatus.ThroatNotFound;
    }

    /// <summary>
    /// u²/a² at a solved station, or subsonic (0) when u² is not positive (BOOT.md, 2026-09-28, finding F4): such a
    /// trial lies at or above the chamber's enthalpy, and the search steps toward lower pressure rather than
    /// stopping. False only when the sound speed itself is not usable, the one case that still ends the search.
    /// </summary>
    internal static bool TryRatio(double chamberEnthalpy, in MixtureState state, out double ratio)
    {
        var soundSquared = state.SoundSpeed * state.SoundSpeed;
        if (!(soundSquared > 0.0))
        {
            ratio = 0.0;
            return false;
        }

        var velocitySquared = StationFigures.VelocitySquared(chamberEnthalpy, in state);
        ratio = velocitySquared > 0.0 ? velocitySquared / soundSquared : 0.0;
        return true;
    }

    /// <summary>
    /// A fingerprint of the condensed species in solution (the equilibrium scratch's <c>CondensedInSolution</c>),
    /// order-independent and sensitive to both the count and the identity of the slots in use, so that a single-phase
    /// state and a pinned pair of the same substance never collide (BOOT.md, the plateau-edge acceptance).
    /// </summary>
    private const long FingerprintModulus = 100_000_007L;

    internal static long CondensedFingerprint(in RocketContext context)
    {
        var slots = context.Scratch.CondensedInSolution;
        var count = 0L;
        var sumOfSquares = 0L;
        for (var i = 0; i < slots.Length; i++)
        {
            var species = slots[i];
            if (species < 0)
            {
                continue;
            }

            count++;
            var key = species + 1L;
            sumOfSquares += key * key;
        }

        return count * FingerprintModulus + sumOfSquares;
    }
}
