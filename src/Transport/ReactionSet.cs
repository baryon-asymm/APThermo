namespace APThermo.Transport;

/// <summary>
/// Stages 8 and 9 of the evaluation: one reaction forming each non-component species of the set from the components, and the
/// trace eliminations — a species of the set below <see cref="TransportSolver.TraceFraction"/> is eliminated from every
/// reaction through the first one that holds it, which is then dropped (the rule of CEA2; see the ⚠ of <c>BOOT.md</c> on the
/// reference's no-op).
/// Scratch: reads <c>Basis</c>, <c>Xs</c>, <c>RowActive</c>, <c>Component</c>, <c>IndexList</c>; writes <c>IsComponent</c>,
/// <c>CompLocal</c>, <c>CompRow</c>, <c>Alpha</c> and <c>Stx</c> (the normalised pivot row, valid only inside one elimination).
/// </summary>
internal static class ReactionSet
{
    private const int Stride = TransportSolver.Stride;

    /// <summary>
    /// Builds the reactions among the <paramref name="nm"/> species of the set and returns how many are left after the trace
    /// eliminations; the count and the number of eliminations go into <paramref name="figures"/>.
    /// </summary>
    internal static int Build(in StationInputs inputs, int nm, ref TransportFigures figures)
    {
        var ncomp = Components(in inputs, nm);
        var nr = Reactions(in inputs, nm, ncomp);
        nr = EliminateTraces(in inputs, nm, nr, ref figures);
        figures.ReactionCount = nr;
        return nr;
    }

    /// <summary>The components of the set: the component species of each active row, each taken once.</summary>
    private static int Components(in StationInputs inputs, int nm)
    {
        var scratch = inputs.Scratch;
        var ncomp = 0;
        for (var a = 0; a < nm; a++)
        {
            scratch.IsComponent[a] = 0;
        }

        for (var i = 0; i < inputs.Species.ElementCount; i++)
        {
            if (scratch.RowActive[i] == 0 || scratch.Component[i] < 0)
            {
                continue;
            }

            var a = ReactionBasis.LocalIndex(in inputs, nm, scratch.Component[i]);
            if (a < 0 || scratch.IsComponent[a] == 1)
            {
                continue;
            }

            scratch.CompLocal[ncomp] = a;
            scratch.CompRow[ncomp] = i;
            scratch.IsComponent[a] = 1;
            ncomp++;
        }

        return ncomp;
    }

    /// <summary>One reaction per non-component species of the set, formed from the components through the reduced basis.</summary>
    private static int Reactions(in StationInputs inputs, int nm, int ncomp)
    {
        var scratch = inputs.Scratch;
        var nr = 0;
        if (ncomp <= 0 || ncomp >= nm)
        {
            return nr;
        }

        for (var a = 0; a < nm; a++)
        {
            if (scratch.IsComponent[a] == 1)
            {
                continue;
            }

            for (var b = 0; b < nm; b++)
            {
                scratch.Alpha[nr * Stride + b] = 0.0;
            }

            scratch.Alpha[nr * Stride + a] = -1.0;
            for (var k = 0; k < ncomp; k++)
            {
                scratch.Alpha[nr * Stride + scratch.CompLocal[k]] = scratch.Basis[scratch.CompRow[k] * Stride + a];
            }

            nr++;
        }

        return nr;
    }

    /// <summary>Removes every trace species of the set from the reactions, and returns how many reactions are left.</summary>
    private static int EliminateTraces(in StationInputs inputs, int nm, int nr, ref TransportFigures figures)
    {
        var scratch = inputs.Scratch;
        var traceEliminations = 0;
        for (var a = 0; a < nm; a++)
        {
            if (scratch.Xs[a] >= TransportSolver.TraceFraction)
            {
                continue;
            }

            var pivotRow = EliminateFrom(in inputs, nm, nr, a);
            if (pivotRow < 0)
            {
                continue;
            }

            DropRow(in inputs, nm, nr, pivotRow);
            nr--;
            traceEliminations++;
        }

        figures.TraceEliminations = traceEliminations;
        return nr;
    }

    /// <summary>Eliminates species <paramref name="a"/> from every reaction through the first one that holds it: that reaction,
    /// normalised by the coefficient of <paramref name="a"/>, is kept in Stx and subtracted from each later one; returns its row.</summary>
    private static int EliminateFrom(in StationInputs inputs, int nm, int nr, int a)
    {
        var scratch = inputs.Scratch;
        var pivotRow = -1;
        for (var r = 0; r < nr; r++)
        {
            var coefficient = scratch.Alpha[r * Stride + a];
            if (Math.Abs(coefficient) <= TransportSolver.EliminationThreshold)
            {
                continue;
            }

            if (pivotRow < 0)
            {
                pivotRow = r;
                for (var b = 0; b < nm; b++)
                {
                    scratch.Stx[b] = scratch.Alpha[r * Stride + b] / coefficient;
                }
            }
            else
            {
                for (var b = 0; b < nm; b++)
                {
                    scratch.Alpha[r * Stride + b] = scratch.Alpha[r * Stride + b] / coefficient - scratch.Stx[b];
                }
            }
        }

        return pivotRow;
    }

    /// <summary>Drops the pivot reaction by moving every later one up.</summary>
    private static void DropRow(in StationInputs inputs, int nm, int nr, int pivotRow)
    {
        var scratch = inputs.Scratch;
        for (var r = pivotRow; r < nr - 1; r++)
        {
            for (var b = 0; b < nm; b++)
            {
                scratch.Alpha[r * Stride + b] = scratch.Alpha[(r + 1) * Stride + b];
            }
        }
    }
}
