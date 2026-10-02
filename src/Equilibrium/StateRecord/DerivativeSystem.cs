using APThermo.Equilibrium.Condensed;
using APThermo.Thermo;

namespace APThermo.Equilibrium.StateRecord;

/// <summary>
/// The equilibrium derivatives of RP-1311 section 2.5 at the converged composition: the tp-shaped matrix solved with the two
/// right-hand sides of tables 2.3 (temperature) and 2.4 (pressure), and the reaction part of cp/R of equation (2.59).
/// Kernel-compatible; it reuses the iteration's matrix, right-hand side and row scales.
/// </summary>
/// <remarks>
/// At a pinned pair — two records of one formula in the solution at their transition — the constant-pressure derivatives do
/// not exist (section 3.5; Gordon 1970). The system is then assembled once, at constant temperature, with one record of the
/// pair left out as the representative of both, and the state carries the reference's plateau convention (BOOT.md, API.md).
/// </remarks>
internal static class DerivativeSystem
{
    /// <summary>
    /// Solves for (∂ln n/∂ln T)_p and (∂ln n/∂ln p)_T, and for the reaction sum; <c>Solved</c> is false when a system was
    /// singular. Reads rule A's tie from <paramref name="state"/>, not from a parameter of its own (BOOT.md, 2026-09-28):
    /// a tie active at the converged composition fixes the tied element's derivative at zero, the same way an absent
    /// element's derivative is fixed by <see cref="CloseRows"/>. Trusts <see cref="IterationState.Tie"/>'s own
    /// <c>Active</c> flag rather than re-deriving it with <see cref="Newton.ElementCoupling.Coupled"/> (the third pass of
    /// 2026-09-28, finding F1): the caller's release-and-restore keeps a tie active exactly at the composition where
    /// the coupling test found the pair told apart — that is why the release was tried — so a recheck here would
    /// undo the "closed with the tie in force" restore for no gain, while agreeing with the caller in every other case
    /// (the tie is only ever set or cleared right before a settlement this method's own composition also sees).
    /// </summary>
    public static Derivatives Solve(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                    in IterationState state, int stride)
    {
        var condensedCount = state.CondensedCount;
        var pairSecond = PairSecond(table, scratch, condensedCount);
        var derivatives = new Derivatives { Pinned = pairSecond >= 0, Solved = true };
        var derivativeCount = condensedCount;
        if (derivatives.Pinned)
        {
            // The representative goes into the last slot, so that the rows and the pivoting keep the order they have
            // without a pair; the caller's order is restored before the return.
            Swap(scratch, pairSecond, condensedCount - 1);
            derivativeCount = condensedCount - 1;
        }

        var layout = new SystemLayout(ProblemKind.AssignedTemperaturePressure, table.ElementCount, derivativeCount, stride);
        var tie = state.Tie;
        var tied = tie.Active ? tie.Element : -1;
        for (var pass = derivatives.Pinned ? 1 : 0; pass < 2; pass++)
        {
            var kind = pass == 0 ? DerivativeKind.Temperature : DerivativeKind.Pressure;
            Assemble(table, scratch, result, layout, kind);
            if (tied >= 0)
            {
                PinTiedRow(scratch, layout, tied);
            }

            if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, layout.Unknowns, layout.Stride))
            {
                derivatives.Solved = false;
                break;
            }

            if (kind == DerivativeKind.Temperature)
            {
                derivatives.DlnNdlnT = scratch.RightHandSide[layout.NRow];
                derivatives.Reaction = Reaction(table, scratch, result, layout, derivatives.DlnNdlnT);
            }
            else
            {
                derivatives.DlnNdlnP = scratch.RightHandSide[layout.NRow];
            }
        }

        if (derivatives.Pinned)
        {
            Swap(scratch, pairSecond, condensedCount - 1);
        }

        return derivatives;
    }

    /// <summary>A tie that survives to the close gives its element's row a unit derivative row, fixing that multiplier's derivative at zero (BOOT.md, rule A, "Derivatives").</summary>
    private static void PinTiedRow(in EquilibriumScratch scratch, in SystemLayout layout, int tied)
    {
        var stride = layout.Stride;
        for (var c = 0; c < layout.Unknowns; c++)
        {
            scratch.Matrix[tied * stride + c] = 0.0;
        }

        scratch.Matrix[tied * stride + tied] = 1.0;
        scratch.RightHandSide[tied] = 0.0;
    }

    /// <summary>The slot of the second record of the first pair of one formula in the solution; −1 when there is no pair.</summary>
    private static int PairSecond(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount)
    {
        for (var c = 0; c < condensedCount; c++)
        {
            for (var d = c + 1; d < condensedCount; d++)
            {
                if (PhaseGeometry.SameFormula(table, scratch.CondensedInSolution[c], scratch.CondensedInSolution[d]))
                {
                    return d;
                }
            }
        }

        return -1;
    }

    private static void Swap(in EquilibriumScratch scratch, int first, int second) =>
        (scratch.CondensedInSolution[first], scratch.CondensedInSolution[second]) = (scratch.CondensedInSolution[second], scratch.CondensedInSolution[first]);

    /// <summary>The tp-shaped matrix at the converged composition with the right-hand side of table 2.3 or of table 2.4.</summary>
    private static void Assemble(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                 in SystemLayout layout, DerivativeKind kind)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = layout.ElementCount;
        var stride = layout.Stride;
        var nRow = layout.NRow;
        for (var k = 0; k < layout.Unknowns * stride; k++)
        {
            scratch.Matrix[k] = 0.0;
        }

        for (var k = 0; k < layout.Unknowns; k++)
        {
            scratch.RightHandSide[k] = 0.0;
        }

        for (var j = 0; j < table.GasCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            var weight = kind == DerivativeKind.Temperature ? -scratch.HOverRT[j] : 1.0;
            for (var k = 0; k < elementCount; k++)
            {
                var akj = table.Stoichiometry[k * speciesCount + j];
                if (akj == 0.0)
                {
                    continue;
                }

                var akjn = akj * nj;
                for (var i = 0; i < elementCount; i++)
                {
                    scratch.Matrix[k * stride + i] += akjn * table.Stoichiometry[i * speciesCount + j];
                }

                scratch.Matrix[k * stride + nRow] += akjn;
                scratch.RightHandSide[k] += akjn * weight;
            }

            scratch.RightHandSide[nRow] += nj * weight;
        }

        CloseRows(table, scratch, layout, kind);
    }

    /// <summary>The Δln n row, the unit rows of the absent elements and the condensed rows. At convergence Σ n_j − n vanishes, so the n-row diagonal is zero.</summary>
    private static void CloseRows(in SpeciesTableView table, in EquilibriumScratch scratch,
                                  in SystemLayout layout, DerivativeKind kind)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = layout.ElementCount;
        var stride = layout.Stride;
        var nRow = layout.NRow;
        for (var i = 0; i < elementCount; i++)
        {
            scratch.Matrix[nRow * stride + i] = scratch.Matrix[i * stride + nRow];
            if (scratch.ElementActive[i] == 0)
            {
                scratch.Matrix[i * stride + i] = 1.0;
                scratch.RightHandSide[i] = 0.0;
            }
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var row = elementCount + c;
            for (var i = 0; i < elementCount; i++)
            {
                var aij = table.Stoichiometry[i * speciesCount + j];
                scratch.Matrix[row * stride + i] = aij;
                scratch.Matrix[i * stride + row] = aij;
            }

            scratch.RightHandSide[row] = kind == DerivativeKind.Temperature ? -scratch.HOverRT[j] : 0.0;
        }
    }

    /// <summary>Equation (2.59): the reaction part of cp/R, from the temperature derivatives just solved for.</summary>
    private static double Reaction(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                   in SystemLayout layout, double dlnNdlnT)
    {
        var speciesCount = table.SpeciesCount;
        var gasCount = table.GasCount;
        var reaction = 0.0;
        for (var i = 0; i < layout.ElementCount; i++)
        {
            var sum = 0.0;
            for (var j = 0; j < gasCount; j++)
            {
                sum += table.Stoichiometry[i * speciesCount + j] * result.Moles[j] * scratch.HOverRT[j];
            }

            reaction += sum * scratch.RightHandSide[i];
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            reaction += scratch.HOverRT[scratch.CondensedInSolution[c]] * scratch.RightHandSide[layout.ElementCount + c];
        }

        var gasEnthalpy = 0.0;
        for (var j = 0; j < gasCount; j++)
        {
            gasEnthalpy += result.Moles[j] * scratch.HOverRT[j];
        }

        // The parenthesis is the report's grouping and the code's before the decomposition: the two last terms are summed
        // first and added to the element and condensed sums once, so that no rounding moves.
        return reaction + (gasEnthalpy * dlnNdlnT + HSquared(table, scratch, result));
    }

    /// <summary>Σ n_j (h_j°/RT)² over the gaseous species: the last term of equation (2.59).</summary>
    private static double HSquared(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result)
    {
        var hSquared = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            hSquared += nj * scratch.HOverRT[j] * scratch.HOverRT[j];
        }

        return hSquared;
    }
}
