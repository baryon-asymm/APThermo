using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// The reduced Newton system of RP-1311 tables 2.1 and 2.2, with the gaseous corrections of equation (2.18) substituted:
/// one method per family of rows, accumulated in the order the report writes them. Kernel-compatible.
/// </summary>
/// <remarks>
/// Unknowns, in this order: the Lagrange multipliers π_i of the elements, the mole-number corrections Δn_j of the condensed
/// species in the solution, Δln n, and Δln T for hp and sp. The matrix is symmetric except for the temperature row, which
/// weighs a species by its enthalpy for hp and by its entropy in the mixture for sp (BOOT.md, Constraints).
/// </remarks>
internal static class IterationMatrix
{
    /// <summary>Fills the matrix and the right-hand side of one Newton step for the current estimate.</summary>
    public static void Assemble(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, in SystemLayout layout, in MixtureSums sums)
    {
        Clear(scratch, layout);
        AccumulateGaseous(table, scratch, result, layout, sums);
        CloseTotalMolesRow(scratch, layout, sums);
        ElementRows(table, problem, scratch, result, layout);
        CondensedRows(table, scratch, result, layout);
        TargetRow(problem, scratch, layout, sums);
    }

    private static void Clear(in EquilibriumScratch scratch, in SystemLayout layout)
    {
        for (var k = 0; k < layout.Unknowns * layout.Stride; k++)
        {
            scratch.Matrix[k] = 0.0;
        }

        for (var k = 0; k < layout.Unknowns; k++)
        {
            scratch.RightHandSide[k] = 0.0;
        }
    }

    /// <summary>Contributions of the gaseous species, accumulated species by species: the element block, the Δln n column and the temperature row.</summary>
    private static void AccumulateGaseous(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                          in SystemLayout layout, in MixtureSums sums)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = layout.ElementCount;
        var stride = layout.Stride;
        var nRow = layout.NRow;
        var tRow = layout.TRow;
        for (var j = 0; j < table.GasCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            var h = scratch.HOverRT[j];
            var s = scratch.SOverR[j];
            var mu = scratch.GOverRT[j] + scratch.LogMoles[j] - sums.LogN + sums.LogPressure;
            // The entropy row weighs a gaseous species by its entropy in the mixture, mixing terms included.
            var tWeight = layout.IsTp ? 0.0 : (layout.IsHp ? h : s - (scratch.LogMoles[j] - sums.LogN) - sums.LogPressure);
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
                scratch.RightHandSide[k] += akjn * mu;
                if (!layout.IsTp)
                {
                    scratch.Matrix[k * stride + tRow] += akjn * h;
                    scratch.Matrix[tRow * stride + k] += akjn * tWeight;
                }
            }

            scratch.RightHandSide[nRow] += nj * mu;
            if (!layout.IsTp)
            {
                scratch.Matrix[nRow * stride + tRow] += nj * h;
                scratch.Matrix[tRow * stride + nRow] += nj * tWeight;
                scratch.Matrix[tRow * stride + tRow] += nj * scratch.CpOverR[j] + nj * tWeight * h;
                scratch.RightHandSide[tRow] += nj * tWeight * mu;
            }
        }
    }

    /// <summary>The Δln n row: its π coefficients are the Δln n column of the element rows, and its diagonal is Σ n_j − n.</summary>
    private static void CloseTotalMolesRow(in EquilibriumScratch scratch, in SystemLayout layout, in MixtureSums sums)
    {
        var stride = layout.Stride;
        var nRow = layout.NRow;
        for (var i = 0; i < layout.ElementCount; i++)
        {
            scratch.Matrix[nRow * stride + i] = scratch.Matrix[i * stride + nRow];
        }

        scratch.Matrix[nRow * stride + nRow] = sums.SumGas - sums.N;
        scratch.RightHandSide[nRow] += sums.N - sums.SumGas;
    }

    /// <summary>Element rows: the residual b_i° − b_i on the right. An absent element keeps a unit row, so that the system stays regular.</summary>
    private static void ElementRows(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, in SystemLayout layout)
    {
        var stride = layout.Stride;
        for (var k = 0; k < layout.ElementCount; k++)
        {
            if (scratch.ElementActive[k] == 0)
            {
                scratch.Matrix[k * stride + k] = 1.0;
                scratch.RightHandSide[k] = 0.0;
                if (!layout.IsTp)
                {
                    scratch.Matrix[layout.TRow * stride + k] = 0.0;
                }

                continue;
            }

            scratch.RightHandSide[k] += problem.ElementMoles[k] - ElementBalance.Abundance(table, result, k);
        }
    }

    /// <summary>One row and one column per condensed species in the solution: its stoichiometry, with g_j/RT on the right.</summary>
    private static void CondensedRows(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                      in SystemLayout layout)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = layout.ElementCount;
        var stride = layout.Stride;
        var tRow = layout.TRow;
        for (var c = 0; c < layout.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var row = elementCount + c;
            var h = scratch.HOverRT[j];
            var tWeight = layout.IsTp ? 0.0 : (layout.IsHp ? h : scratch.SOverR[j]);
            for (var i = 0; i < elementCount; i++)
            {
                var aij = table.Stoichiometry[i * speciesCount + j];
                scratch.Matrix[row * stride + i] = aij;
                scratch.Matrix[i * stride + row] = aij;
            }

            scratch.RightHandSide[row] = scratch.GOverRT[j];
            if (!layout.IsTp)
            {
                scratch.Matrix[row * stride + tRow] = h;
                scratch.Matrix[tRow * stride + row] = tWeight;
                scratch.Matrix[tRow * stride + tRow] += result.Moles[j] * scratch.CpOverR[j];
            }
        }
    }

    /// <summary>The right-hand side of the temperature row: the assigned enthalpy (table 2.1) or entropy (table 2.2) against the mixture's.</summary>
    private static void TargetRow(in EquilibriumProblem problem, in EquilibriumScratch scratch, in SystemLayout layout, in MixtureSums sums)
    {
        if (layout.IsHp)
        {
            scratch.RightHandSide[layout.TRow] += problem.Target / (PhysicalConstants.R * sums.Temperature) - sums.HOverRT;
        }
        else if (!layout.IsTp)
        {
            scratch.RightHandSide[layout.TRow] += problem.Target / PhysicalConstants.R - sums.SOverR + sums.N - sums.SumGas;
        }
    }
}
