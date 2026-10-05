using APThermo.Thermo;

namespace APThermo.Equilibrium.GasPhase;

/// <summary>
/// The tangent-plane test of the gas phase at the multipliers of a condensed minimum: an ideal gas of any composition can
/// lower the Gibbs energy if and only if S = Σ_j exp(Σ_i a_ij π_i − g_j/RT − ln(p/p°)) over the gases exceeds 1. Where the
/// optimal multipliers are not unique, the face they span is searched for a point with S below 1. Kernel-compatible; the
/// scratch it uses is the one <see cref="CondensedSimplex"/> leaves (BOOT.md): the face directions in the matrix past its
/// first <c>ElementCount</c> rows of stride, their coordinates in <c>Tie.CondensedMoles</c>, the coordinates' upper bounds in
/// the row scales.
/// </summary>
internal static class TangentPlane
{
    /// <summary>ln S below −this is a certificate; above this the gas is required; between, undecided.</summary>
    internal const double Margin = 1.0e-9;

    /// <summary>Bound on each face coordinate: e^200 in a partial pressure is far past any decision.</summary>
    private const double FaceBound = 200.0;

    /// <summary>Sweeps of the coordinate search over the face.</summary>
    private const int FaceSweeps = 32;

    /// <summary>Bisections of one coordinate's line search.</summary>
    private const int LineBisections = 100;

    /// <summary>Offset of the face directions V (m × d, row-major by m) in the matrix scratch, past every row a solve touches.</summary>
    public static int DirectionOffset(in SpeciesTableView table) => table.ElementCount * ScratchLayout.MaxUnknowns(table.ElementCount);

    /// <summary>
    /// The face of the multipliers that prove the condensed minimum optimal: one direction v_k = B⁻ᵀ e_q per basic slot q at
    /// zero level (a condensed column at zero moles, or the artificial column of a redundant row), stored in the matrix
    /// scratch, with the upper bound of its coordinate in the row scales (0 for a condensed q, whose own gain is the
    /// coordinate; the face bound for an artificial one). The coordinates start at zero. Returns d, or −1 if a solve failed.
    /// </summary>
    public static int Directions(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, int m)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        var offset = DirectionOffset(table);
        var d = 0;
        for (var q = 0; q < m; q++)
        {
            if (!CondensedSimplex.ZeroLevel(problem, scratch, q))
            {
                continue;
            }

            CondensedSimplex.AssembleBasis(table, scratch, m, stride, transpose: true);
            for (var r = 0; r < m; r++)
            {
                scratch.RightHandSide[r] = r == q ? 1.0 : 0.0;
            }

            if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, m, stride))
            {
                return -1;
            }

            for (var r = 0; r < m; r++)
            {
                scratch.Matrix[offset + r * m + d] = scratch.RightHandSide[r];
            }

            d++;
        }

        // The row scales are free once the last solve is done: the coordinates' upper bounds, in slot order.
        var k = 0;
        for (var q = 0; q < m; q++)
        {
            if (CondensedSimplex.ZeroLevel(problem, scratch, q))
            {
                scratch.RowScale[k] = scratch.CondensedInSolution[q] >= 0 ? 0.0 : FaceBound;
                scratch.Tie.CondensedMoles[k] = 0.0;
                k++;
            }
        }

        return d;
    }

    /// <summary>
    /// A search of the face for a certificate: coordinate by coordinate, the convex ln S is minimized along v_k between the
    /// bounds that keep every eligible condensed gain ≤ 0, until ln S falls below the margin or the sweeps run out. Any
    /// point of the face with S below 1 proves the condensed minimum is the equilibrium, so the search need not find the
    /// exact minimum; a search that stops above 1 decides nothing (the caller keeps the failure). Returns ln S at the
    /// multipliers it leaves in <c>result.Multipliers</c>.
    /// </summary>
    public static double Search(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in Face face)
    {
        var logS = FaceWrite(table, scratch, result, face);
        for (var sweep = 0; sweep < FaceSweeps && !(logS < -Margin); sweep++)
        {
            for (var k = 0; k < face.Directions; k++)
            {
                var range = CoordinateBounds(table, scratch, result, face, k);
                if (!(range.Low <= range.High))
                {
                    continue;
                }

                scratch.Tie.CondensedMoles[k] = LineMinimum(table, scratch, result, face, k, range);
                logS = FaceWrite(table, scratch, result, face);
            }
        }

        return logS;
    }

    /// <summary>ln Σ_j exp(Σ_i a_ij π_i − g_j/RT − ln(p/p°)) over the gases whose elements are present, at the multipliers in <c>result.Multipliers</c>.</summary>
    public static double LogTangentSum(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure)
    {
        var largest = LargestExponent(table, scratch, result, logPressure);
        var sum = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent)
            {
                sum += KernelMath.Exp(GasExponent(table, scratch, result, j, logPressure) - largest);
            }
        }

        return largest + KernelMath.Log(sum);
    }

    /// <summary>π = π0 + V t into the result's multipliers; returns ln S there.</summary>
    private static double FaceWrite(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in Face face)
    {
        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        for (var r = 0; r < face.Rows; r++)
        {
            var pi = scratch.Tie.LogMoles[r];
            for (var k = 0; k < face.Directions; k++)
            {
                pi += scratch.Matrix[face.Offset + r * face.Rows + k] * scratch.Tie.CondensedMoles[k];
            }

            result.Multipliers[scratch.Tie.CondensedSet[r]] = pi;
        }

        return LogTangentSum(table, scratch, result, face.LogPressure);
    }

    /// <summary>a_j·v_k for any species over the active rows.</summary>
    private static double Along(in SpeciesTableView table, in EquilibriumScratch scratch, in Face face, int j, int k)
    {
        var c = 0.0;
        for (var r = 0; r < face.Rows; r++)
        {
            c += CondensedSimplex.Entry(table, scratch, j, r) * scratch.Matrix[face.Offset + r * face.Rows + k];
        }

        return c;
    }

    /// <summary>The interval of coordinate k that keeps every eligible condensed record outside the basis at a gain of at most zero.</summary>
    private static CoordinateRange CoordinateBounds(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                                    in Face face, int k)
    {
        var t = scratch.Tie.CondensedMoles[k];
        var lo = -FaceBound;
        var hi = scratch.RowScale[k];
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            if (!CondensedSimplex.Eligible(table, scratch, j, face.Temperature) || CondensedSimplex.InBasis(scratch, face.Rows, j))
            {
                continue;
            }

            var c = Along(table, scratch, face, j, k);
            var gain = CondensedGain(table, scratch, result, j);
            if (c > CondensedSimplex.PivotTolerance)
            {
                hi = KernelMath.Min(hi, t - gain / c);
            }
            else if (c < -CondensedSimplex.PivotTolerance)
            {
                lo = KernelMath.Max(lo, t - gain / c);
            }
        }

        return new CoordinateRange(lo, hi);
    }

    private static double CondensedGain(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int j)
    {
        var gain = -scratch.GOverRT[j];
        for (var i = 0; i < table.ElementCount; i++)
        {
            gain += table.Stoichiometry[i * table.SpeciesCount + j] * result.Multipliers[i];
        }

        return gain;
    }

    /// <summary>The minimum of the convex ln S along v_k over the range, by bisection on its slope; leaves t_k as it found it.</summary>
    private static double LineMinimum(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                      in Face face, int k, CoordinateRange range)
    {
        var t = scratch.Tie.CondensedMoles[k];
        var best = Bisect(table, scratch, result, face, k, range);
        scratch.Tie.CondensedMoles[k] = t;
        return best;
    }

    /// <summary>The end of the range the slope points away from, or the point inside it where the slope changes sign.</summary>
    private static double Bisect(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                 in Face face, int k, CoordinateRange range)
    {
        if (!(Slope(table, scratch, result, face, k, range.Low) < 0.0))
        {
            return range.Low;
        }

        if (!(Slope(table, scratch, result, face, k, range.High) > 0.0))
        {
            return range.High;
        }

        var a = range.Low;
        var b = range.High;
        for (var n = 0; n < LineBisections; n++)
        {
            var mid = 0.5 * (a + b);
            if (Slope(table, scratch, result, face, k, mid) > 0.0)
            {
                b = mid;
            }
            else
            {
                a = mid;
            }
        }

        return 0.5 * (a + b);
    }

    /// <summary>d ln S / d t_k with t_k set to <paramref name="value"/>: the S-weighted mean of a_j·v_k over the gases.</summary>
    private static double Slope(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                in Face face, int k, double value)
    {
        scratch.Tie.CondensedMoles[k] = value;
        _ = FaceWrite(table, scratch, result, face);
        var largest = LargestExponent(table, scratch, result, face.LogPressure);
        var weight = 0.0;
        var moment = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.Of(scratch, j) == SpeciesMark.Absent)
            {
                continue;
            }

            var w = KernelMath.Exp(GasExponent(table, scratch, result, j, face.LogPressure) - largest);
            weight += w;
            moment += w * Along(table, scratch, face, j, k);
        }

        return moment / weight;
    }

    /// <summary>The largest exponent over the gases whose elements are present.</summary>
    private static double LargestExponent(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure)
    {
        var largest = double.NegativeInfinity;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent)
            {
                largest = KernelMath.Max(largest, GasExponent(table, scratch, result, j, logPressure));
            }
        }

        return largest;
    }

    /// <summary>Σ_i a_ij π_i − g_j/RT − ln(p/p°) for a gas.</summary>
    private static double GasExponent(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int j, double logPressure)
    {
        var e = -scratch.GOverRT[j] - logPressure;
        for (var i = 0; i < table.ElementCount; i++)
        {
            e += table.Stoichiometry[i * table.SpeciesCount + j] * result.Multipliers[i];
        }

        return e;
    }
}
