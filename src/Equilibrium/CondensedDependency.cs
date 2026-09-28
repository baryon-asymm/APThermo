using APThermo.Thermo;

namespace APThermo.Equilibrium;

/// <summary>
/// Rule B (BOOT.md, "Two rules come before the remedies above", 2026-09-28): a condensed set whose last species is a
/// linear combination of the others. The species that entered last stays; the favourable reaction that forms it from
/// the others consumes the ones with a positive coefficient, and the one it exhausts first — the smallest n_p/c_p
/// over the positive coefficients, the simplex ratio test — is the one that leaves. Kernel-compatible; uses the dead
/// Newton matrix as workspace, since the matrix that just failed carries no live iterate.
/// </summary>
internal static class CondensedDependency
{
    /// <summary>Per element: the least-squares residual of the combination must be at most this, in kmol per kg.</summary>
    private const double ResidualLimit = 1.0e-9;

    /// <summary>A coefficient at or below this is not positive enough to be a candidate of the ratio test.</summary>
    private const double CoefficientFloor = 1.0e-9;

    /// <summary>
    /// The position, in <c>scratch.CondensedInSolution</c>, of the species the ratio test removes; −1 when the species
    /// entered last is not a linear combination of the other condensed species of the solution.
    /// </summary>
    public static int LeavingPosition(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount)
    {
        var others = condensedCount - 1;
        if (others < 1)
        {
            return -1;
        }

        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        var entering = scratch.CondensedInSolution[others];
        for (var p = 0; p < others; p++)
        {
            var jp = scratch.CondensedInSolution[p];
            for (var q = 0; q < others; q++)
            {
                scratch.Matrix[p * stride + q] = Dot(table, jp, scratch.CondensedInSolution[q]);
            }

            scratch.RightHandSide[p] = Dot(table, jp, entering);
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, others, stride))
        {
            return -1;
        }

        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var residual = table.Stoichiometry[i * speciesCount + entering];
            for (var p = 0; p < others; p++)
            {
                residual -= scratch.RightHandSide[p] * table.Stoichiometry[i * speciesCount + scratch.CondensedInSolution[p]];
            }

            if (Math.Abs(residual) > ResidualLimit)
            {
                return -1;
            }
        }

        var best = -1;
        var bestRatio = double.PositiveInfinity;
        for (var p = 0; p < others; p++)
        {
            var c = scratch.RightHandSide[p];
            if (c <= CoefficientFloor)
            {
                continue;
            }

            var ratio = result.Moles[scratch.CondensedInSolution[p]] / c;
            if (ratio < bestRatio)
            {
                bestRatio = ratio;
                best = p;
            }
        }

        return best;
    }

    /// <summary>The element-vector dot product of two species, over every element of the table.</summary>
    private static double Dot(in SpeciesTableView table, int j, int k)
    {
        var speciesCount = table.SpeciesCount;
        var sum = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            sum += table.Stoichiometry[i * speciesCount + j] * table.Stoichiometry[i * speciesCount + k];
        }

        return sum;
    }
}
