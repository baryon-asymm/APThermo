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

    /// <summary>The plateau-edge re-solve's outer reach in ln p (BOOT.md, 2026-09-28, the third pass): a retry at
    /// an offset beyond this, from <see cref="RocketSolver.ThroatBracketWidth"/>, is never tried.</summary>
    private const double MaxEdgeOffset = 1.0e-4;

    /// <summary>The geometric growth of the plateau-edge re-solve's offset (BOOT.md, 2026-09-28, the third pass):
    /// each retry's offset in ln p is this many times the previous one's, the first retry starting at
    /// <see cref="RocketSolver.ThroatBracketWidth"/> itself. Eight linear steps of the bracket width never reached
    /// the single-phase side of the Li/O/H bands this replaces (the third pass's own measurement, BOOT.md); this
    /// growth reaches an order of magnitude past it within a handful of retries.</summary>
    private const double EdgeOffsetGrowth = 4.0;

    /// <summary>A fixed bound on the number of geometric retries, well above what <see cref="MaxEdgeOffset"/> ever
    /// lets run: kernel code needs a fixed iteration cap, and the offset check inside the loop is what actually
    /// stops the search once it would exceed <see cref="MaxEdgeOffset"/>.</summary>
    private const int MaxEdgeAttempts = 16;

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
        // its limit at gamma_s = 1 exactly, where (6.15) itself is KernelMath.Pow(1, +-infinity) = 1, the chamber pressure
        // (BOOT.md, 2026-09-28, finding F4): the equilibrium node's plateau convention for an undissociated gas.
        var pressureCandidate = Math.Abs(gammaChamber - 1.0) <= RocketSolver.GammaOneTolerance
            ? chamber.Pressure * KernelMath.Exp(-0.5)
            : chamber.Pressure / KernelMath.Pow(0.5 * (gammaChamber + 1.0), gammaChamber / (gammaChamber - 1.0));
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
            if (Math.Abs(ratio - 1.0) <= RocketSolver.ThroatDecisionTolerance)
            {
                // The decision: the fixed tail of momentum steps follows it, whatever their ratio, and may run past
                // the iteration cap (BOOT.md, 2026-10-02, the owner's decision).
                converged = true;
                var tailStatus = Tail(in query, ref bracket, pressureSolved, ratio, out var pressureTail, out _);
                pressureSolved = pressureTail;
                return tailStatus;
            }

            pressureCandidate = NextPressure(pressureSolved, in state, ratio);
            temperatureEstimate = state.Temperature;
        }

        return CaseStatus.Ok;
    }

    /// <summary>
    /// Equation (6.17): the momentum relation from a solved trial to the sonic point. The division is grouped before
    /// the multiplication, as the original `pressureThroat *= a / b` compiled it, so a converging case's bits are
    /// unchanged (BOOT.md, Structure: every expression keeps its present order). A non-positive u² (ratio 0, BOOT.md
    /// finding F4) steps the pressure down by the same formula, instead of ending the search without a bracket.
    /// </summary>
    private static double NextPressure(double pressure, in MixtureState state, double ratio) =>
        pressure * ((1.0 + state.GammaS * ratio) / (1.0 + state.GammaS));

    /// <summary>
    /// The fixed tail after the throat's decision (BOOT.md, 2026-10-02, the owner's decision): exactly
    /// <see cref="RocketSolver.ThroatTailSteps"/> momentum steps (6.17) from the decided trial, whatever their ratio,
    /// the last being the throat. The count is the constant's, never the 20-iteration cap's, so the tail is bounded
    /// on its own. Every solved trial is tracked in the bracket. <paramref name="setKept"/> is false when a step
    /// left the condensed set the decided trial held; a failed solve ends the tail with its status.
    /// </summary>
    private static CaseStatus Tail(in ThroatQuery query, ref ThroatBracket bracket, double pressureDecided, double ratioDecided,
                                   out double pressureEnd, out bool setKept)
    {
        var context = query.Context;
        var decidedFingerprint = CondensedFingerprint(in context);
        var state = context.Result.Stations[RocketSolver.Throat];
        var pressure = pressureDecided;
        var ratio = ratioDecided;
        pressureEnd = pressureDecided;
        setKept = true;
        for (var step = 0; step < RocketSolver.ThroatTailSteps; step++)
        {
            var candidate = NextPressure(pressure, in state, ratio);
            var request = new StationRequest(RocketSolver.Throat, candidate, state.Temperature, query.Chamber.Entropy, query.Flow);
            var status = Trial(in query, ref bracket, in request, out state, out ratio);
            if (status != CaseStatus.Ok)
            {
                return status;
            }

            pressure = candidate;
            pressureEnd = candidate;
            setKept = setKept && CondensedFingerprint(in context) == decidedFingerprint;
        }

        return CaseStatus.Ok;
    }

    /// <summary>One solved trial of the tail or of the bisection's fallback: the station, its u²/a² (an unusable
    /// sound speed is <see cref="CaseStatus.ThroatNotFound"/>, as in the bisection), and its place in the bracket.</summary>
    private static CaseStatus Trial(in ThroatQuery query, ref ThroatBracket bracket, in StationRequest request, out MixtureState state, out double ratio)
    {
        var context = query.Context;
        state = default;
        ratio = 0.0;
        if (!StationSolve.At(in context, in request))
        {
            return (CaseStatus)context.Result.StationStatus[RocketSolver.Throat];
        }

        state = context.Result.Stations[RocketSolver.Throat];
        if (!TryRatio(query.Chamber.Enthalpy, in state, out ratio))
        {
            return CaseStatus.ThroatNotFound;
        }

        bracket.Track(request.Pressure, state.Temperature, ratio, CondensedFingerprint(in context));
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

            var midPressure = KernelMath.Exp(0.5 * (KernelMath.Log(bracket.SubsonicPressure) + KernelMath.Log(bracket.SupersonicPressure)));
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

            var fingerprint = CondensedFingerprint(in context);
            var oneSet = bracket.SubsonicFingerprint == bracket.SupersonicFingerprint && fingerprint == bracket.SubsonicFingerprint;
            bracket.Track(midPressure, state.Temperature, ratio, fingerprint);
            lastRatio = ratio;
            pressureSolved = midPressure;
            if (oneSet && Math.Abs(ratio - 1.0) <= RocketSolver.ThroatDecisionTolerance)
            {
                return DecideBisected(in query, ref bracket, in request, ratio, out pressureSolved);
            }
        }

        return Math.Abs(lastRatio - 1.0) <= RocketSolver.SonicTolerance ? CaseStatus.Ok : CaseStatus.ThroatNotFound;
    }

    /// <summary>
    /// The bisection's decision, taken where both ends of the bracket held one condensed set (BOOT.md, 2026-10-02, the
    /// owner's decision): the same tail of momentum steps from the midpoint's state. A step that changes the
    /// condensed set, or whose solve fails, leaves the midpoint where the decision was made as the throat, solved
    /// again so the buffers hold it.
    /// </summary>
    private static CaseStatus DecideBisected(in ThroatQuery query, ref ThroatBracket bracket, in StationRequest decided, double ratio, out double pressureSolved)
    {
        var status = Tail(in query, ref bracket, decided.Pressure, ratio, out var pressureTail, out var setKept);
        if (status == CaseStatus.Ok && setKept)
        {
            pressureSolved = pressureTail;
            return CaseStatus.Ok;
        }

        pressureSolved = decided.Pressure;
        return Trial(in query, ref bracket, in decided, out _, out _);
    }

    /// <summary>
    /// Accepts the plateau edge: the single-phase state on the chamber side of the edge, re-solved so the buffers
    /// hold it. Seeded from the chamber's own (single-phase) composition, not whatever the bisection's last trial
    /// left in the throat's row, which may sit on the pinned-pair side of the edge (found while writing this
    /// criterion's evidence: an unseeded re-solve here converged to the pinned pair even at a genuinely single-phase
    /// pressure). The re-solved state is accepted only when it lands back on the bracket's own subsonic side (BOOT.md,
    /// 2026-09-28, finding F3); otherwise the search steps further toward the chamber, at ln p offsets growing
    /// geometrically from <see cref="RocketSolver.ThroatBracketWidth"/> (BOOT.md, 2026-09-28, the third pass), until
    /// a solve lands on the subsonic side or the next offset would exceed <see cref="MaxEdgeOffset"/>. No landing
    /// within that reach is <see cref="CaseStatus.ThroatNotFound"/>.
    /// </summary>
    internal static CaseStatus AcceptPlateauEdge(in ThroatQuery query, in ThroatBracket bracket, ref double pressureSolved)
    {
        var context = query.Context;
        var chamber = query.Chamber;
        var offset = 0.0;
        for (var attempt = 0; attempt < MaxEdgeAttempts; attempt++)
        {
            var pressure = bracket.SubsonicPressure * KernelMath.Exp(offset);
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

            var nextOffset = attempt == 0 ? RocketSolver.ThroatBracketWidth : offset * EdgeOffsetGrowth;
            if (nextOffset > MaxEdgeOffset)
            {
                break;
            }

            offset = nextOffset;
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
