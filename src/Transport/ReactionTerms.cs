using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Transport;

/// <summary>
/// Stage 12 of the evaluation: the reaction contribution to the conductivity and to the heat capacity of the set, Butler and
/// Brokaw over the pairs of the set (RP-1311 equations (5.8) to (5.12)). The same quadratic form twice: without the diffusion
/// weights for the heat capacity, with RT/(pD_ij) for the conductivity.
/// Scratch: reads <c>IndexList</c>, <c>Xs</c>, <c>H</c>, <c>Eta</c>; writes <c>Alpha</c> (the coefficients below the threshold
/// are cleaned to zero), <c>DeltaH</c>, <c>Matrix</c>, <c>MatrixReacting</c>, <c>Rhs</c>, <c>RowScale</c> and <c>Stx</c> (the
/// per-pair difference vector, valid only inside one pair).
/// </summary>
internal static class ReactionTerms
{
    private const int Stride = TransportSolver.Stride;

    /// <summary>
    /// The two contributions of the <paramref name="nr"/> reactions among the <paramref name="nm"/> species of the set, both
    /// zero when there is no reaction. When a system cannot be solved the contributions are zero and the status is
    /// <see cref="CaseStatus.SingularMatrix"/>, so that the station keeps its frozen figures (<c>API.md</c>, Errors).
    /// </summary>
    internal static ReactionContribution Evaluate(in StationInputs inputs, int nm, int nr)
    {
        if (nr <= 0)
        {
            return new ReactionContribution(0.0, 0.0, CaseStatus.Ok);
        }

        Enthalpies(in inputs, nm, nr);
        Accumulate(in inputs, nm, nr);
        Symmetrise(in inputs, nr);
        return Solve(in inputs, nr);
    }

    /// <summary>The enthalpy difference of every reaction, with the reaction coefficients cleaned, and the two matrices zeroed.</summary>
    private static void Enthalpies(in StationInputs inputs, int nm, int nr)
    {
        var scratch = inputs.Scratch;
        for (var r = 0; r < nr; r++)
        {
            var deltaH = 0.0;
            for (var b = 0; b < nm; b++)
            {
                if (Math.Abs(scratch.Alpha[r * Stride + b]) < TransportSolver.ReactionCoefficientThreshold)
                {
                    scratch.Alpha[r * Stride + b] = 0.0;
                }

                deltaH += scratch.Alpha[r * Stride + b] * scratch.H[b];
            }

            scratch.DeltaH[r] = deltaH;
            for (var c = 0; c < nr; c++)
            {
                scratch.Matrix[r * Stride + c] = 0.0;
                scratch.MatrixReacting[r * Stride + c] = 0.0;
            }
        }
    }

    /// <summary>The upper triangles of the two matrices, summed over the pairs of the set that are not traces.</summary>
    private static void Accumulate(in StationInputs inputs, int nm, int nr)
    {
        var scratch = inputs.Scratch;
        for (var k = 0; k < nm - 1; k++)
        {
            if (scratch.Xs[k] < TransportSolver.TraceFraction)
            {
                continue;
            }

            for (var m = k + 1; m < nm; m++)
            {
                if (scratch.Xs[m] < TransportSolver.TraceFraction)
                {
                    continue;
                }

                AccumulatePair(in inputs, nr, k, m);
            }
        }
    }

    /// <summary>One pair of the set: its difference vector over the reactions, and its term in both matrices.</summary>
    private static void AccumulatePair(in StationInputs inputs, int nr, int k, int m)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var massK = species.MolarMass[scratch.IndexList[k]];
        var massM = species.MolarMass[scratch.IndexList[m]];
        var rtOverPD = 5.0 * massK * massM / (3.0 * TransportSolver.AStar * scratch.Eta[k * Stride + m] * (massK + massM));
        var inverse = 1.0 / (scratch.Xs[k] * scratch.Xs[m]);
        for (var r = 0; r < nr; r++)
        {
            var alphaK = scratch.Alpha[r * Stride + k];
            var alphaM = scratch.Alpha[r * Stride + m];
            scratch.Stx[r] = alphaK == 0.0 && alphaM == 0.0 ? 0.0 : scratch.Xs[m] * alphaK - scratch.Xs[k] * alphaM;
        }

        for (var r = 0; r < nr; r++)
        {
            var stxR = scratch.Stx[r];
            if (stxR == 0.0)
            {
                continue;
            }

            for (var c = r; c < nr; c++)
            {
                var term = stxR * scratch.Stx[c] * inverse;
                scratch.Matrix[r * Stride + c] += term;
                scratch.MatrixReacting[r * Stride + c] += term * rtOverPD;
            }
        }
    }

    /// <summary>The lower triangle of both matrices from the upper one, and the right-hand side of the first solve.</summary>
    private static void Symmetrise(in StationInputs inputs, int nr)
    {
        var scratch = inputs.Scratch;
        for (var r = 0; r < nr; r++)
        {
            for (var c = 0; c < r; c++)
            {
                scratch.Matrix[r * Stride + c] = scratch.Matrix[c * Stride + r];
                scratch.MatrixReacting[r * Stride + c] = scratch.MatrixReacting[c * Stride + r];
            }

            scratch.Rhs[r] = scratch.DeltaH[r];
        }
    }

    /// <summary>The two dense solves: ΔHᵀ G⁻¹ ΔH without the diffusion weights, then with them.</summary>
    private static ReactionContribution Solve(in StationInputs inputs, int nr)
    {
        var scratch = inputs.Scratch;
        if (!DenseSolver.Solve(scratch.Matrix, scratch.Rhs, scratch.RowScale, nr, Stride))
        {
            return new ReactionContribution(0.0, 0.0, CaseStatus.SingularMatrix);
        }

        var heatCapacity = 0.0;
        for (var r = 0; r < nr; r++)
        {
            heatCapacity += scratch.DeltaH[r] * scratch.Rhs[r];
            scratch.Rhs[r] = scratch.DeltaH[r];
        }

        heatCapacity *= PhysicalConstants.R;
        if (!DenseSolver.Solve(scratch.MatrixReacting, scratch.Rhs, scratch.RowScale, nr, Stride))
        {
            // Both contributions fall away, not only the conductivity: on SingularMatrix the station keeps its frozen figures
            // (API.md, Errors; the decision of 2026-09-14 in BOOT.md, ## Structure).
            return new ReactionContribution(0.0, 0.0, CaseStatus.SingularMatrix);
        }

        var conductivity = 0.0;
        for (var r = 0; r < nr; r++)
        {
            conductivity += scratch.DeltaH[r] * scratch.Rhs[r];
        }

        conductivity *= PhysicalConstants.R;
        return new ReactionContribution(heatCapacity, conductivity, CaseStatus.Ok);
    }
}
