using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// The linear system of one trace-gas step (BOOT.md, "The unknowns and rows"). The unknowns are the corrections <c>dπ_i</c> of
/// the multipliers, <c>u_c = dn_c/n</c> of the condensed species of the solution, <c>δ = dn/n</c> of the gaseous moles and, for
/// hp and sp, <c>τ = d ln T</c>; the gases themselves are substituted exactly through <c>x_j(π)</c>. Every element row is
/// divided by <c>n</c> and the condensed and total-moles unknowns are carried relative to it, so the matrix does not depend
/// on <c>n</c> and stays regular as the gas vanishes. Kernel-compatible; it uses the matrix, right-hand side and row scales
/// of the scratch and reads <c>Corrections</c> as ln x_j (<see cref="TraceGasStep.Fractions"/>).
/// </summary>
internal static class TraceGasSystem
{
    /// <summary>The ridge on the multipliers' block, relative to the row's largest entry: a direction of π whose curvature comes only from gases below this share of the gas is held, not solved.</summary>
    private const double Ridge = 1.0e-13;

    /// <summary>Assembles and solves the step's system at <paramref name="frame"/>; false when it is singular. The solution is in the right-hand side.</summary>
    public static bool Solve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, in TraceGasFrame frame)
    {
        Assemble(table, problem, scratch, result, frame);
        if (frame.Layout.IsTp)
        {
            return DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, frame.Layout.Unknowns, frame.Layout.Stride);
        }

        EnergyRow(table, problem, scratch, result, frame);
        var scale = ScaleTemperatureColumn(scratch, frame.Layout);
        var solved = DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, frame.Layout.Unknowns, frame.Layout.Stride);
        scratch.RightHandSide[frame.Layout.TRow] /= scale;
        return solved;
    }

    /// <summary>
    /// Divides the temperature column by its largest entry when that exceeds one, and returns the divisor: the unknown becomes
    /// <c>scale · τ</c>. The column holds the enthalpies h/RT of the species (tens to hundreds), the rest of the matrix numbers of
    /// order one, and a direction of π that only trace gases carry sits at 1e-10 beside them: left unscaled, its pivot falls under the
    /// solver's 1e-13 of the row's largest entry although the system is regular.
    /// </summary>
    private static double ScaleTemperatureColumn(in EquilibriumScratch scratch, in SystemLayout layout)
    {
        var largest = 1.0;
        for (var r = 0; r < layout.Unknowns; r++)
        {
            largest = KernelMath.Max(largest, Math.Abs(scratch.Matrix[r * layout.Stride + layout.TRow]));
        }

        for (var r = 0; r < layout.Unknowns; r++)
        {
            scratch.Matrix[r * layout.Stride + layout.TRow] /= largest;
        }

        return largest;
    }

    /// <summary>
    /// The element rows over n, the condensed stationarities and the phase sum. An absent element is a unit row; a ridge of
    /// <see cref="Ridge"/> times the row's largest entry sits on the diagonal of every active element row.
    /// </summary>
    public static void Assemble(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, in TraceGasFrame frame)
    {
        var layout = frame.Layout;
        var speciesCount = table.SpeciesCount;
        var elementCount = layout.ElementCount;
        var stride = layout.Stride;
        var nRow = layout.NRow;
        for (var k = 0; k < layout.Unknowns * stride; k++)
        {
            scratch.Matrix[k] = 0.0;
        }

        for (var j = 0; j < table.GasCount; j++)
        {
            if (!SpeciesMarks.InPlay(scratch, j))
            {
                continue;
            }

            var x = Math.Exp(scratch.Corrections[j]);
            for (var k = 0; k < elementCount; k++)
            {
                var akj = table.Stoichiometry[k * speciesCount + j];
                if (akj == 0.0)
                {
                    continue;
                }

                var akjx = akj * x;
                for (var i = 0; i < elementCount; i++)
                {
                    scratch.Matrix[k * stride + i] += akjx * table.Stoichiometry[i * speciesCount + j];
                }

                scratch.Matrix[k * stride + nRow] += akjx;
                scratch.Matrix[nRow * stride + k] += akjx / frame.Sum;
            }
        }

        ElementRows(table, problem, scratch, result, frame);
        CondensedRows(table, scratch, result, layout);
        scratch.RightHandSide[nRow] = -Math.Log(frame.Sum);
    }

    /// <summary>The right-hand side <c>(b_i − Σ_j a_ij n_j)/n</c> of every element row, the ridge on its diagonal, the unit row of an absent element.</summary>
    private static void ElementRows(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, in TraceGasFrame frame)
    {
        var stride = frame.Layout.Stride;
        for (var k = 0; k < frame.Layout.ElementCount; k++)
        {
            if (scratch.ElementActive[k] == 0)
            {
                scratch.Matrix[k * stride + k] = 1.0;
                scratch.RightHandSide[k] = 0.0;
                continue;
            }

            scratch.RightHandSide[k] = (problem.ElementMoles[k] - ElementBalance.Abundance(table, result, k)) / frame.N;
            var rowScale = 0.0;
            for (var l = 0; l < frame.Layout.Unknowns; l++)
            {
                rowScale = KernelMath.Max(rowScale, Math.Abs(scratch.Matrix[k * stride + l]));
            }

            scratch.Matrix[k * stride + k] += Ridge * rowScale;
        }
    }

    /// <summary>The stationarity <c>Σ_i a_ic dπ_i = g_c/RT − Σ_i a_ic π_i</c> of every condensed species of the solution, and its column in the element rows.</summary>
    private static void CondensedRows(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                      in SystemLayout layout)
    {
        var speciesCount = table.SpeciesCount;
        for (var c = 0; c < layout.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var row = layout.ElementCount + c;
            var residual = scratch.GOverRT[j];
            for (var i = 0; i < layout.ElementCount; i++)
            {
                var aij = table.Stoichiometry[i * speciesCount + j];
                scratch.Matrix[row * layout.Stride + i] = aij;
                scratch.Matrix[i * layout.Stride + row] = aij;
                residual -= aij * result.Multipliers[i];
            }

            scratch.RightHandSide[row] = residual;
        }
    }

    /// <summary>
    /// The temperature column and the energy row of hp and sp: the enthalpy (hp) or entropy (sp) of the mixture at its
    /// assigned value, in the same unknowns, with <c>τ = d ln T</c>.
    /// </summary>
    private static void EnergyRow(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                  in EquilibriumResult result, in TraceGasFrame frame)
    {
        var layout = frame.Layout;
        var stride = layout.Stride;
        var tRow = layout.TRow;
        var hp = layout.IsHp;
        var diagonal = 0.0;
        var energy = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!SpeciesMarks.InPlay(scratch, j))
            {
                continue;
            }

            var logFraction = scratch.Corrections[j];
            var x = Math.Exp(logFraction);
            var h = scratch.HOverRT[j];
            var w = hp ? h : scratch.SOverR[j] - logFraction - frame.LogPressure;
            var dpiWeight = hp ? h : w - 1.0;
            GasColumn(table, scratch, frame, j, x * h, frame.N * x * dpiWeight);
            scratch.Matrix[layout.NRow * stride + tRow] += x * h / frame.Sum;
            scratch.Matrix[tRow * stride + layout.NRow] += frame.N * x * w;
            diagonal += frame.N * x * (scratch.CpOverR[j] + h * dpiWeight);
            energy += frame.N * x * w;
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var h = scratch.HOverRT[j];
            var w = hp ? h : scratch.SOverR[j];
            scratch.Matrix[(layout.ElementCount + c) * stride + tRow] = h;
            scratch.Matrix[tRow * stride + layout.ElementCount + c] = frame.N * w;
            diagonal += result.Moles[j] * scratch.CpOverR[j];
            energy += result.Moles[j] * w;
        }

        for (var k = 0; k < layout.ElementCount; k++)
        {
            if (scratch.ElementActive[k] == 0)
            {
                scratch.Matrix[k * stride + tRow] = 0.0;
                scratch.Matrix[tRow * stride + k] = 0.0;
            }
        }

        scratch.Matrix[tRow * stride + tRow] = diagonal;
        scratch.RightHandSide[tRow] = hp
            ? problem.Target / (PhysicalConstants.R * frame.Temperature) - energy
            : problem.Target / PhysicalConstants.R - energy;
    }

    /// <summary>
    /// Adds the gas <paramref name="j"/>'s share of the temperature column of the element rows (<paramref name="column"/>, per
    /// element coefficient) and of the energy row's multiplier columns (<paramref name="row"/>, per element coefficient).
    /// </summary>
    private static void GasColumn(in SpeciesTableView table, in EquilibriumScratch scratch, in TraceGasFrame frame, int j,
                                  double column, double row)
    {
        for (var k = 0; k < frame.Layout.ElementCount; k++)
        {
            var akj = table.Stoichiometry[k * table.SpeciesCount + j];
            if (akj == 0.0)
            {
                continue;
            }

            scratch.Matrix[k * frame.Layout.Stride + frame.Layout.TRow] += akj * column;
            scratch.Matrix[frame.Layout.TRow * frame.Layout.Stride + k] += akj * row;
        }
    }
}
