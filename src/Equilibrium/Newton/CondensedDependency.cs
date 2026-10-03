using APThermo.Thermo;

namespace APThermo.Equilibrium.Newton;

/// <summary>
/// Rule B (BOOT.md, "Two rules come before the remedies above", 2026-09-28): a condensed set whose last species is a
/// linear combination of the others. The species that entered last stays; the favourable reaction that forms it from
/// the others consumes the ones with a positive coefficient, and the one it exhausts first — the smallest n_p/c_p
/// over the positive coefficients, the simplex ratio test — is the one that leaves. When the others alone do not
/// form it, the gas phase counts as one more column of the combination, with the composition of its retained
/// species, and never leaves: at an assigned temperature and pressure the gas is one phase of the set, and the phase
/// rule is what the singular matrix reports (BOOT.md, 2026-10-03). Kernel-compatible; uses the dead Newton matrix as
/// workspace, since the matrix that just failed carries no live iterate.
/// </summary>
internal static class CondensedDependency
{
    /// <summary>Per element: the least-squares residual of the combination must be at most this, in kmol per kg.</summary>
    private const double ResidualLimit = 1.0e-9;

    /// <summary>A coefficient at or below this is not positive enough to be a candidate of the ratio test.</summary>
    private const double CoefficientFloor = 1.0e-9;

    /// <summary>
    /// The position, in <c>scratch.CondensedInSolution</c>, of the species the ratio test removes; −1 when the species
    /// entered last is not a linear combination of the other condensed species of the solution, alone or with the gas
    /// phase.
    /// </summary>
    public static int LeavingPosition(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount)
    {
        var others = condensedCount - 1;
        if (others < 1)
        {
            return -1;
        }

        var found = Combines(table, scratch, result, others, 0.0);
        if (!found)
        {
            var gasMoles = 0.0;
            for (var j = 0; j < table.GasCount; j++)
            {
                gasMoles += result.Moles[j];
            }

            found = gasMoles > 0.0 && Combines(table, scratch, result, others, gasMoles);
        }

        return found ? RatioTest(scratch, result, others) : -1;
    }

    /// <summary>
    /// Least squares of the species entered last on the other condensed species of the solution and, when
    /// <paramref name="gasMoles"/> is positive, on the gas phase's composition per mole of its retained species as one
    /// more column; true when the residual is within <see cref="ResidualLimit"/> for every element. The coefficients of
    /// the condensed species are left in <c>scratch.RightHandSide[0..others)</c>.
    /// </summary>
    private static bool Combines(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int others, double gasMoles)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        var entering = scratch.CondensedInSolution[others];
        var withGas = gasMoles > 0.0;
        for (var p = 0; p < others; p++)
        {
            var jp = scratch.CondensedInSolution[p];
            for (var q = 0; q < others; q++)
            {
                scratch.Matrix[p * stride + q] = Dot(table, jp, scratch.CondensedInSolution[q]);
            }

            scratch.RightHandSide[p] = Dot(table, jp, entering);
            if (withGas)
            {
                var d = GasDot(table, result, jp, gasMoles);
                scratch.Matrix[p * stride + others] = d;
                scratch.Matrix[others * stride + p] = d;
            }
        }

        if (withGas)
        {
            scratch.Matrix[others * stride + others] = GasGas(table, result, gasMoles);
            scratch.RightHandSide[others] = GasDot(table, result, entering, gasMoles);
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, withGas ? others + 1 : others, stride))
        {
            return false;
        }

        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var residual = table.Stoichiometry[i * speciesCount + entering];
            for (var p = 0; p < others; p++)
            {
                residual -= scratch.RightHandSide[p] * table.Stoichiometry[i * speciesCount + scratch.CondensedInSolution[p]];
            }

            if (withGas)
            {
                residual -= scratch.RightHandSide[others] * GasComponent(table, result, i, gasMoles);
            }

            if (Math.Abs(residual) > ResidualLimit)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The simplex ratio test over the positive coefficients of the condensed species; −1 when none is positive.</summary>
    private static int RatioTest(in EquilibriumScratch scratch, in EquilibriumResult result, int others)
    {
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

    /// <summary>Element <paramref name="i"/> of the gas phase per mole of its retained species, Σ_j a_ij n_j / Σ_j n_j.</summary>
    private static double GasComponent(in SpeciesTableView table, in EquilibriumResult result, int i, double gasMoles)
    {
        var speciesCount = table.SpeciesCount;
        var sum = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            sum += table.Stoichiometry[i * speciesCount + j] * result.Moles[j];
        }

        return sum / gasMoles;
    }

    /// <summary>The element-vector dot product of a species with the gas phase's composition.</summary>
    private static double GasDot(in SpeciesTableView table, in EquilibriumResult result, int k, double gasMoles)
    {
        var speciesCount = table.SpeciesCount;
        var sum = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            sum += table.Stoichiometry[i * speciesCount + k] * GasComponent(table, result, i, gasMoles);
        }

        return sum;
    }

    /// <summary>The gas phase's composition dotted with itself.</summary>
    private static double GasGas(in SpeciesTableView table, in EquilibriumResult result, double gasMoles)
    {
        var sum = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var g = GasComponent(table, result, i, gasMoles);
            sum += g * g;
        }

        return sum;
    }
}
