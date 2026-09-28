namespace APThermo.Performance.Tests;

/// <summary>
/// The mass-flux oracle of the second hidden-defect audit's finding F1 (BOOT.md, 2026-09-28, "the throat is the
/// first maximum of the mass flux met from the chamber"): rho*u along the chamber isentrope, from sp solves each
/// seeded fresh from the chamber's own composition (the audit's own method, so a discontinuous jump across a
/// melting plateau is never masked by warm-starting a probe across it), over a grid of pressure ratios p/p_c the
/// audit itself used (121 points, 0.30 to 0.90), with each local maximum refined by golden-section search. Used
/// only by the tests that need it; no production code reads it.
/// </summary>
internal static class MassFluxOracle
{
    /// <summary>The grid's bounds and point count (the audit's own: 121 points, p/p_c 0.30 to 0.90).</summary>
    public const double GridLow = 0.30;
    public const double GridHigh = 0.90;
    public const int GridPoints = 121;

    private const int GoldenSteps = 60;
    private static readonly double GoldenSection = (Math.Sqrt(5.0) - 1.0) / 2.0;

    /// <summary>rho*u at one pressure ratio p/p_c, an sp solve on <paramref name="probeStation"/>, seeded fresh
    /// from the chamber's own composition; false when the solve did not converge.</summary>
    public static bool TryFlux(in RocketContext context, in ChamberReference chamber, StationFlow flow, int probeStation, double ratio, out double flux)
    {
        StationSolve.CopyComposition(in context, RocketSolver.Chamber, probeStation);
        var temperatureEstimate = context.Result.Stations[RocketSolver.Chamber].Temperature;
        var request = new StationRequest(probeStation, chamber.Pressure * ratio, temperatureEstimate, chamber.Entropy, flow);
        if (!StationSolve.At(in context, in request))
        {
            flux = double.NaN;
            return false;
        }

        var state = context.Result.Stations[probeStation];
        flux = state.Density * StationFigures.Velocity(chamber.Enthalpy, in state);
        return true;
    }

    /// <summary>The grid's own ratios and fluxes (NaN where a probe did not solve).</summary>
    public static (double[] Ratios, double[] Fluxes) Scan(in RocketContext context, in ChamberReference chamber, StationFlow flow, int probeStation)
    {
        var ratios = new double[GridPoints];
        var fluxes = new double[GridPoints];
        for (var i = 0; i < GridPoints; i++)
        {
            var ratio = GridLow + (GridHigh - GridLow) * i / (GridPoints - 1);
            ratios[i] = ratio;
            fluxes[i] = TryFlux(in context, in chamber, flow, probeStation, ratio, out var flux) ? flux : double.NaN;
        }

        return (ratios, fluxes);
    }

    /// <summary>Every interior grid index that is a local maximum of <paramref name="fluxes"/> (strictly greater
    /// than both neighbours; a NaN neighbour never qualifies its side).</summary>
    public static IEnumerable<int> LocalMaxima(double[] fluxes)
    {
        for (var i = 1; i < fluxes.Length - 1; i++)
        {
            if (fluxes[i] > fluxes[i - 1] && fluxes[i] > fluxes[i + 1])
            {
                yield return i;
            }
        }
    }

    /// <summary>Refines the flux maximum bracketed by the grid points around index <paramref name="peak"/> by
    /// golden-section search; returns its ratio and flux.</summary>
    public static (double Ratio, double Flux) Refine(in RocketContext context, in ChamberReference chamber, StationFlow flow,
                                                      int probeStation, double[] ratios, int peak)
    {
        var low = ratios[peak - 1];
        var high = ratios[peak + 1];
        var left = high - GoldenSection * (high - low);
        var right = low + GoldenSection * (high - low);
        _ = TryFlux(in context, in chamber, flow, probeStation, left, out var fluxLeft);
        _ = TryFlux(in context, in chamber, flow, probeStation, right, out var fluxRight);
        for (var step = 0; step < GoldenSteps; step++)
        {
            if (fluxLeft < fluxRight)
            {
                low = left;
                left = right;
                fluxLeft = fluxRight;
                right = low + GoldenSection * (high - low);
                _ = TryFlux(in context, in chamber, flow, probeStation, right, out fluxRight);
            }
            else
            {
                high = right;
                right = left;
                fluxRight = fluxLeft;
                left = high - GoldenSection * (high - low);
                _ = TryFlux(in context, in chamber, flow, probeStation, left, out fluxLeft);
            }
        }

        return fluxLeft > fluxRight ? (left, fluxLeft) : (right, fluxRight);
    }
}
