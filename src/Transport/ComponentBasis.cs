namespace APThermo.Transport;

/// <summary>
/// Stage 3 of the evaluation (2026-09-28): the components settled before the set is seeded. cea 3.3.4 reduces its full
/// stoichiometry over every active element row before any transport species is chosen and stores the reverted list
/// (<c>equilibrium.f90:2264-2291</c>); this stage reaches the same verdict — which row's component survives and which
/// reverts — over a compact matrix of just the components' and the defaults' columns, one pair per active row, which is
/// enough because a row's pivot is always one of those two species and every row operation of the reduction only ever
/// touches such columns (<c>BOOT.md</c>, Constraints, the ⚠ of 2026-09-28).
/// A row is processed in element order: its component's column is the pivot; when that entry has been driven to zero by
/// an earlier row's elimination — its column was proportional to that earlier row's pivot column — the row takes back its
/// default species and the default's column becomes the pivot instead; the row is left unreduced only when that pivot is
/// zero too. <see cref="TransportSetSelection"/> then seeds every settled component, whatever its moles.
/// Scratch: reads <c>RowActive</c>, <c>Default</c>; reads and writes <c>Component</c>; reuses <c>Basis</c> as a small
/// working matrix before <see cref="ReactionBasis"/> fills it for the transport set — nothing earlier in the evaluation
/// reads <c>Basis</c>, and nothing later reads what this stage leaves there. The compact matrix needs two columns per
/// active row, at most <c>2 · Thermo.TableLimits.MaxElements</c> = 40 = <see cref="TransportSolver.MaxSpecies"/>, so it
/// always fits the row width the scratch already reserves.
/// </summary>
internal static class ComponentBasis
{
    private const int Stride = TransportSolver.Stride;

    /// <summary>Settles every active row's component before the transport set is seeded.</summary>
    internal static void Settle(in StationInputs inputs)
    {
        Fill(in inputs);
        Eliminate(in inputs);
    }

    /// <summary>
    /// Writes, for every active row, the stoichiometry of every active row's default (even column) and current component
    /// (odd column) at a compact column pair per active row, in element order.
    /// </summary>
    private static void Fill(in StationInputs inputs)
    {
        var scratch = inputs.Scratch;
        var elementCount = inputs.Species.ElementCount;
        for (var i = 0; i < elementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            var rank = 0;
            for (var k = 0; k < elementCount; k++)
            {
                if (scratch.RowActive[k] == 0)
                {
                    continue;
                }

                scratch.Basis[i * Stride + 2 * rank] = StoichiometryAt(in inputs, i, scratch.Default[k]);
                scratch.Basis[i * Stride + 2 * rank + 1] = StoichiometryAt(in inputs, i, scratch.Component[k]);
                rank++;
            }
        }
    }

    /// <summary>Row <paramref name="row"/>'s stoichiometry at <paramref name="columnSpecies"/>, or zero when the row has no
    /// default or component species at all (no gas of the case carries its element).</summary>
    private static double StoichiometryAt(in StationInputs inputs, int row, int columnSpecies)
    {
        if (columnSpecies < 0)
        {
            return 0.0;
        }

        var species = inputs.Species;
        return species.Stoichiometry[row * species.SpeciesCount + columnSpecies];
    }

    /// <summary>Gauss-Jordan over every active row's component column; a vanished pivot reverts to the default column.</summary>
    private static void Eliminate(in StationInputs inputs)
    {
        var scratch = inputs.Scratch;
        var elementCount = inputs.Species.ElementCount;
        var width = 2 * ActiveRowCount(in inputs);
        for (var i = 0; i < elementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            var rank = RankOf(in inputs, i);
            var column = 2 * rank + 1;
            var pivot = scratch.Basis[i * Stride + column];
            if (pivot == 0.0)
            {
                scratch.Component[i] = scratch.Default[i];
                column = 2 * rank;
                pivot = scratch.Basis[i * Stride + column];
                if (pivot == 0.0)
                {
                    continue;
                }
            }

            if (pivot != 1.0)
            {
                for (var a = 0; a < width; a++)
                {
                    scratch.Basis[i * Stride + a] /= pivot;
                }
            }

            ClearColumn(in inputs, i, column, width);
        }
    }

    /// <summary>The number of active element rows: the width of the compact matrix is twice this count.</summary>
    private static int ActiveRowCount(in StationInputs inputs)
    {
        var scratch = inputs.Scratch;
        var count = 0;
        for (var i = 0; i < inputs.Species.ElementCount; i++)
        {
            if (scratch.RowActive[i] != 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The count of active rows before <paramref name="row"/>: its compact column pair is (2·rank, 2·rank + 1).</summary>
    private static int RankOf(in StationInputs inputs, int row)
    {
        var scratch = inputs.Scratch;
        var rank = 0;
        for (var i = 0; i < row; i++)
        {
            if (scratch.RowActive[i] != 0)
            {
                rank++;
            }
        }

        return rank;
    }

    /// <summary>Subtracts the pivot row from every other active row at the pivot column, cleaning entries below the
    /// reaction basis's threshold to zero, exactly as <see cref="ReactionBasis"/>'s own elimination does.</summary>
    private static void ClearColumn(in StationInputs inputs, int pivotRow, int column, int width)
    {
        var scratch = inputs.Scratch;
        var elementCount = inputs.Species.ElementCount;
        for (var k = 0; k < elementCount; k++)
        {
            if (k == pivotRow || scratch.RowActive[k] == 0)
            {
                continue;
            }

            var factor = scratch.Basis[k * Stride + column];
            if (factor == 0.0)
            {
                continue;
            }

            for (var a = 0; a < width; a++)
            {
                var value = scratch.Basis[k * Stride + a] - scratch.Basis[pivotRow * Stride + a] * factor;
                scratch.Basis[k * Stride + a] = Math.Abs(value) < TransportSolver.BasisCleaningThreshold ? 0.0 : value;
            }
        }
    }
}
