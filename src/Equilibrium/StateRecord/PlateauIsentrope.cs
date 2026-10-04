using APThermo.Thermo;

namespace APThermo.Equilibrium.StateRecord;

/// <summary>
/// The isentropic system at the converged composition (BOOT.md, "The gas-participating plateau"): the sp-shaped linear
/// system of RP-1311 in the corrections <c>dπ_i</c>, <c>dn_c</c>, <c>d ln n</c> and <c>d ln T</c>, with <c>d ln p = 1</c> on
/// the right-hand side, assembled over the iteration's own scratch. It is non-singular where the constant-temperature system
/// is not (a gas-participating univariant plateau), because the temperature column and the entropy row break the null
/// direction, and it is the better-conditioned route to <c>γ_s</c> where the constant-temperature one cancels
/// (<c>Cp/Cv</c> beyond 1e6). Kernel-compatible; <see cref="DerivativeSystem"/> pins a surviving tie's row and solves.
/// </summary>
internal static class PlateauIsentrope
{
    /// <summary>
    /// Assembles the system for <c>d ln p = 1</c> at constant entropy: the element rows with the gaseous sums, the condensed
    /// rows, the <c>n</c> row and the entropy row of RP-1311's sp iteration. <paramref name="layout"/> is the
    /// constant-temperature layout of the same composition; its element and condensed counts and its stride are used.
    /// </summary>
    public static SystemLayout Assemble(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                        in MixtureSums sums, in SystemLayout layout)
    {
        var system = new SystemLayout(ProblemKind.AssignedEntropyPressure, layout.ElementCount, layout.CondensedCount, layout.Stride, layout.Tie, layout.CarrierLogN);
        for (var k = 0; k < system.Unknowns * system.Stride; k++)
        {
            scratch.Matrix[k] = 0.0;
        }

        for (var k = 0; k < system.Unknowns; k++)
        {
            scratch.RightHandSide[k] = 0.0;
        }

        AddGas(table, scratch, result, sums, system);
        CloseRows(table, scratch, sums, system);
        AddCondensed(table, scratch, system);
        return system;
    }

    /// <summary><c>(∂ln V/∂ln p)_s = d ln n + d ln T − 1</c>, read from the solved system of <see cref="Assemble"/>.</summary>
    public static double DlnVdlnP(in EquilibriumScratch scratch, in SystemLayout system) =>
        scratch.RightHandSide[system.NRow] + scratch.RightHandSide[system.TRow] - 1.0;

    /// <summary>The gaseous species' contributions to the element rows, the <c>n</c> row, the entropy row and the right-hand side.</summary>
    private static void AddGas(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                               in MixtureSums sums, in SystemLayout system)
    {
        var speciesCount = table.SpeciesCount;
        var stride = system.Stride;
        for (var j = 0; j < table.GasCount; j++)
        {
            var nj = system.GasMoles(scratch, result, j);
            if (nj == 0.0)
            {
                continue;
            }

            var h = scratch.HOverRT[j];
            var s = scratch.SOverR[j] - Math.Log(nj) + sums.LogN - sums.LogPressure;
            for (var k = 0; k < system.ElementCount; k++)
            {
                var akj = table.Stoichiometry[k * speciesCount + j];
                if (akj == 0.0)
                {
                    continue;
                }

                var akjn = akj * nj;
                for (var i = 0; i < system.ElementCount; i++)
                {
                    scratch.Matrix[k * stride + i] += akjn * table.Stoichiometry[i * speciesCount + j];
                }

                scratch.Matrix[k * stride + system.NRow] += akjn;
                scratch.Matrix[k * stride + system.TRow] += akjn * h;
                scratch.RightHandSide[k] += akjn;
                scratch.Matrix[system.NRow * stride + k] += akjn;
                scratch.Matrix[system.TRow * stride + k] += akjn * s;
            }

            scratch.Matrix[system.NRow * stride + system.TRow] += nj * h;
            scratch.RightHandSide[system.NRow] += nj;
            scratch.Matrix[system.TRow * stride + system.NRow] += nj * s;
            scratch.Matrix[system.TRow * stride + system.TRow] += nj * s * h;
            scratch.RightHandSide[system.TRow] += nj * s;
        }
    }

    /// <summary>The frozen heat capacity and the gaseous moles on the entropy row, and the unit rows of the absent elements.</summary>
    private static void CloseRows(in SpeciesTableView table, in EquilibriumScratch scratch, in MixtureSums sums, in SystemLayout system)
    {
        scratch.Matrix[system.TRow * system.Stride + system.TRow] += sums.CpOverR;
        scratch.RightHandSide[system.TRow] += sums.SumGas;
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (scratch.ElementActive[i] == 0)
            {
                scratch.Matrix[i * system.Stride + i] = 1.0;
                scratch.RightHandSide[i] = 0.0;
            }
        }
    }

    /// <summary>The condensed rows and columns: the element vector, the enthalpy on the temperature column, the entropy on the entropy row.</summary>
    private static void AddCondensed(in SpeciesTableView table, in EquilibriumScratch scratch, in SystemLayout system)
    {
        var speciesCount = table.SpeciesCount;
        var stride = system.Stride;
        for (var c = 0; c < system.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var row = system.ElementCount + c;
            for (var i = 0; i < system.ElementCount; i++)
            {
                var aij = table.Stoichiometry[i * speciesCount + j];
                scratch.Matrix[row * stride + i] = aij;
                scratch.Matrix[i * stride + row] = aij;
            }

            scratch.Matrix[row * stride + system.TRow] = scratch.HOverRT[j];
            scratch.Matrix[system.TRow * stride + row] = scratch.SOverR[j];
            scratch.RightHandSide[row] = 0.0;
        }
    }
}
