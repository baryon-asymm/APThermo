using APThermo.Thermo;
using ILGPU;

namespace APThermo.Equilibrium.StateRecord;

/// <summary>
/// The equilibrium derivatives of RP-1311 section 2.5 at the converged composition: the tp-shaped matrix solved with the two
/// right-hand sides of tables 2.3 (temperature) and 2.4 (pressure), and the reaction part of cp/R of equation (2.59).
/// Kernel-compatible; it reuses the iteration's matrix, right-hand side and row scales.
/// </summary>
/// <remarks>
/// At a pinned set — condensed species of the solution whose element vectors are linearly dependent: two records of one
/// formula at their transition, or different species at a reaction plateau — the constant-pressure derivatives do not
/// exist (section 3.5; Gordon 1970). The system is then assembled once, at constant temperature, with one species of the set
/// left out as the representative, and the state carries the reference's plateau convention (BOOT.md, API.md).
/// At a gas-participating plateau, where the vectors are independent but a condensed vector lies in the span of the gas
/// composition and the vectors before it, the constant-temperature system is singular and the isentropic one of
/// <see cref="PlateauIsentrope"/> carries <c>γ_s</c>; the same system replaces the constant-temperature route beside a
/// near-univariant composition, where that route cancels.
/// </remarks>
internal static class DerivativeSystem
{
    /// <summary>
    /// Solves for (∂ln n/∂ln T)_p and (∂ln n/∂ln p)_T, and for the reaction sum; <c>Solved</c> is false when a system was
    /// singular and no gas-participating plateau explains it. Reads rule A's tie from <paramref name="state"/>, not from a
    /// parameter of its own (BOOT.md, 2026-09-28):
    /// a tie active at the converged composition fixes the tied element's derivative at zero, the same way an absent
    /// element's derivative is fixed by <see cref="CloseRows"/>. Trusts <see cref="IterationState.Tie"/>'s own
    /// <c>Active</c> flag rather than re-deriving it with <see cref="Newton.ElementCoupling.Coupled"/> (the third pass of
    /// 2026-09-28, finding F1): the caller's release-and-restore keeps a tie active exactly at the composition where
    /// the coupling test found the tied combination of elements no longer held — that is why the release was tried — so a
    /// recheck here would undo the "closed with the tie in force" restore for no gain, while agreeing with the caller in
    /// every other case (the tie is only ever set or cleared right before a settlement this method's own composition also
    /// sees). <paramref name="sums"/> are the sums at the converged composition, which the isentropic system reads.
    /// </summary>
    public static Derivatives Solve(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                    in IterationState state, int stride, in MixtureSums sums)
    {
        var condensedCount = state.CondensedCount;
        var whole = new SystemLayout(ProblemKind.AssignedTemperaturePressure, table.ElementCount, condensedCount, stride, state);
        var representative = DependentSlot(table, scratch, result, whole, gasColumn: false);
        var derivatives = new Derivatives { Pinned = representative >= 0, Solved = true };
        var derivativeCount = condensedCount;
        if (derivatives.Pinned)
        {
            // The representative goes into the last slot, so that the rows and the pivoting keep the order they have
            // without a pinned set; the caller's order is restored before the return.
            Swap(scratch, representative, condensedCount - 1);
            derivativeCount = condensedCount - 1;
        }

        var layout = new SystemLayout(ProblemKind.AssignedTemperaturePressure, table.ElementCount, derivativeCount, stride, state);
        for (var pass = derivatives.Pinned ? 1 : 0; pass < 2; pass++)
        {
            var kind = pass == 0 ? DerivativeKind.Temperature : DerivativeKind.Pressure;
            Assemble(table, scratch, result, layout, kind);
            PinTiedRow(scratch, layout);
            if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, layout.Unknowns, layout.Stride)
                && !(Unexplained(table, scratch, result, layout, derivatives.Pinned)
                     && ScaledRetry(table, scratch, result, layout, kind, sums.SumGas)))
            {
                derivatives = GasParticipatingPlateau(table, scratch, result, sums, layout, derivatives);
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

        if (derivatives.Solved && !derivatives.Pinned && MixtureProperties.IsNearUnivariant(sums, derivatives))
        {
            derivatives.Isentropic = Isentropic(table, scratch, result, sums, layout, out var dlnVdlnPs);
            derivatives.DlnVdlnPIsentropic = dlnVdlnPs;
        }

        if (representative >= 0)
        {
            Swap(scratch, representative, condensedCount - 1);
        }

        return derivatives;
    }

    /// <summary>
    /// The fallback of a singular constant-temperature system with no pinned set: when the gas composition and the condensed
    /// vectors are linearly dependent, the state is a gas-participating plateau and carries the pinned convention, with
    /// <c>(∂ln V/∂ln p)_s</c> from the isentropic system (BOOT.md). Otherwise the system stays singular.
    /// </summary>
    private static Derivatives GasParticipatingPlateau(in SpeciesTableView table, in EquilibriumScratch scratch,
                                                       in EquilibriumResult result, in MixtureSums sums,
                                                       in SystemLayout layout, Derivatives derivatives)
    {
        derivatives.Solved = false;
        if (derivatives.Pinned
            || DependentSlot(table, scratch, result, layout, gasColumn: true) < 0
            || !Isentropic(table, scratch, result, sums, layout, out var dlnVdlnPs))
        {
            return derivatives;
        }

        derivatives.Pinned = true;
        derivatives.Solved = true;
        derivatives.Isentropic = true;
        derivatives.DlnVdlnPIsentropic = dlnVdlnPs;
        derivatives.DlnNdlnT = 0.0;
        derivatives.Reaction = 0.0;
        return derivatives;
    }

    /// <summary>
    /// Whether a singular constant-temperature system is left unexplained by the plateaus: a pinned set already has its
    /// representative out, and otherwise no condensed vector lies in the span of the gas composition and the vectors before it
    /// (the gas-participating plateau, <see cref="GasParticipatingPlateau"/>). Only then is the scaled solve worth a try.
    /// </summary>
    private static bool Unexplained(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                    in SystemLayout layout, bool pinned) =>
        pinned || DependentSlot(table, scratch, result, layout, gasColumn: true) < 0;

    /// <summary>
    /// The same system once more with the condensed columns of the element rows carried relative to the gaseous moles
    /// <paramref name="n"/> (unknowns <c>dn_c/n</c>), the condensed unknowns multiplied back by <paramref name="n"/> after the
    /// solve (BOOT.md, 2026-10-04, for 0.2.2). A trace gas (n of 1e-10 beside condensed species of 1e-2) leaves the element rows'
    /// multiplier pivots below the scale the condensed columns set, and the scaled solve keeps them at their own scale. It runs
    /// only after the plain solve failed, so a state that is solved today is decided before it and no bit of it moves.
    /// </summary>
    private static bool ScaledRetry(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                    in SystemLayout layout, DerivativeKind kind, double n)
    {
        if (!(n > 0.0))
        {
            return false;
        }

        Assemble(table, scratch, result, layout, kind);
        PinTiedRow(scratch, layout);
        for (var c = 0; c < layout.CondensedCount; c++)
        {
            for (var i = 0; i < layout.ElementCount; i++)
            {
                scratch.Matrix[i * layout.Stride + layout.ElementCount + c] *= n;
            }
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, layout.Unknowns, layout.Stride))
        {
            return false;
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            scratch.RightHandSide[layout.ElementCount + c] *= n;
        }

        return true;
    }

    /// <summary>Assembles and solves the isentropic system of <see cref="PlateauIsentrope"/>; false when it is singular.</summary>
    private static bool Isentropic(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                   in MixtureSums sums, in SystemLayout layout, out double dlnVdlnP)
    {
        var system = PlateauIsentrope.Assemble(table, scratch, result, sums, layout);
        PinTiedRow(scratch, system);
        var solved = DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, system.Unknowns, system.Stride);
        dlnVdlnP = solved ? PlateauIsentrope.DlnVdlnP(scratch, system) : 0.0;
        return solved;
    }

    /// <summary>A tie that survives to the close gives its element's row a unit derivative row, fixing that multiplier's derivative at zero (BOOT.md, rule A, "Derivatives"); nothing without a tie.</summary>
    private static void PinTiedRow(in EquilibriumScratch scratch, in SystemLayout layout)
    {
        if (!layout.Tie.Active)
        {
            return;
        }

        var tied = layout.Tie.Element;
        var stride = layout.Stride;
        for (var c = 0; c < layout.Unknowns; c++)
        {
            scratch.Matrix[tied * stride + c] = 0.0;
        }

        scratch.Matrix[tied * stride + tied] = 1.0;
        scratch.RightHandSide[tied] = 0.0;
    }

    /// <summary>Relative residual at or below which a condensed species' element vector lies in the span of the slots before it.</summary>
    private const double DependenceTolerance = 1e-9;

    /// <summary>
    /// The first slot of the condensed set whose element vector is a linear combination of the vectors of the slots before
    /// it (modified Gram–Schmidt over the element rows, in solution order); −1 when the set is independent. A pinned pair
    /// (two records of one formula) is the simplest case, a reaction plateau among different condensed species
    /// (2 Al(OH)3 = Al2O3 + 3 H2O(L)) the general one. With <paramref name="gasColumn"/> the gas composition
    /// <c>g_i = Σ_j a_ij n_j</c> over the gaseous species is the first vector of the basis, and the slot found is one whose
    /// vector lies in the span of the gas and of the slots before it: a gas-participating plateau (CaCO3 = CaO + CO2).
    /// The orthonormal basis is kept in the first rows of <c>scratch.Matrix</c>, a workspace <see cref="Assemble"/> and
    /// <see cref="PlateauIsentrope.Assemble"/> clear before they assemble anything.
    /// </summary>
    private static int DependentSlot(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                     in SystemLayout layout, bool gasColumn)
    {
        var elementCount = table.ElementCount;
        var stride = layout.Stride;
        var first = gasColumn ? 1 : 0;
        if (gasColumn)
        {
            LoadGasVector(table, scratch, result, layout);
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            var row = (c + first) * stride;
            var norm = LoadElementVector(table, scratch, scratch.CondensedInSolution[c], row);
            for (var k = 0; k < c + first; k++)
            {
                RemoveComponent(scratch.Matrix, k * stride, row, elementCount);
            }

            var residual = SquaredNorm(scratch.Matrix, row, elementCount);
            if (residual <= DependenceTolerance * DependenceTolerance * norm)
            {
                return c;
            }

            var scale = 1.0 / Math.Sqrt(residual);
            for (var i = 0; i < elementCount; i++)
            {
                scratch.Matrix[row + i] *= scale;
            }
        }

        return -1;
    }

    /// <summary>Writes the unit vector of the gas composition <c>Σ_j a_ij n_j</c> over the gaseous species at row 0 of the matrix.</summary>
    private static void LoadGasVector(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                      in SystemLayout layout)
    {
        var norm = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var g = 0.0;
            for (var j = 0; j < table.GasCount; j++)
            {
                g += table.Stoichiometry[i * table.SpeciesCount + j] * layout.GasMoles(scratch, result, j);
            }

            scratch.Matrix[i] = g;
            norm += g * g;
        }

        var scale = 1.0 / Math.Sqrt(norm);
        for (var i = 0; i < table.ElementCount; i++)
        {
            scratch.Matrix[i] *= scale;
        }
    }

    /// <summary>Writes the element vector of species <paramref name="j"/> at <paramref name="row"/> of the matrix; returns its squared norm.</summary>
    private static double LoadElementVector(in SpeciesTableView table, in EquilibriumScratch scratch, int j, int row)
    {
        var norm = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var a = table.Stoichiometry[i * table.SpeciesCount + j];
            scratch.Matrix[row + i] = a;
            norm += a * a;
        }

        return norm;
    }

    /// <summary>Subtracts from the vector at <paramref name="row"/> its projection on the unit vector at <paramref name="basis"/>.</summary>
    private static void RemoveComponent(ArrayView<double> matrix, int basis, int row, int length)
    {
        var dot = 0.0;
        for (var i = 0; i < length; i++)
        {
            dot += matrix[basis + i] * matrix[row + i];
        }

        for (var i = 0; i < length; i++)
        {
            matrix[row + i] -= dot * matrix[basis + i];
        }
    }

    private static double SquaredNorm(ArrayView<double> matrix, int row, int length)
    {
        var sum = 0.0;
        for (var i = 0; i < length; i++)
        {
            sum += matrix[row + i] * matrix[row + i];
        }

        return sum;
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
            var nj = layout.GasMoles(scratch, result, j);
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
                sum += table.Stoichiometry[i * speciesCount + j] * layout.GasMoles(scratch, result, j) * scratch.HOverRT[j];
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
            gasEnthalpy += layout.GasMoles(scratch, result, j) * scratch.HOverRT[j];
        }

        // The parenthesis is the report's grouping and the code's before the decomposition: the two last terms are summed
        // first and added to the element and condensed sums once, so that no rounding moves.
        return reaction + (gasEnthalpy * dlnNdlnT + HSquared(table, scratch, result, layout));
    }

    /// <summary>Σ n_j (h_j°/RT)² over the gaseous species: the last term of equation (2.59).</summary>
    private static double HSquared(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                   in SystemLayout layout)
    {
        var hSquared = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            var nj = layout.GasMoles(scratch, result, j);
            if (nj == 0.0)
            {
                continue;
            }

            hSquared += nj * scratch.HOverRT[j] * scratch.HOverRT[j];
        }

        return hSquared;
    }
}
