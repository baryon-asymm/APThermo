namespace APThermo.Transport;

/// <summary>
/// Stage 7 of the evaluation: the stoichiometry of the set reduced so that the column of every component is a unit vector. The
/// row operations act on every column alike, so the reduced rows are the formation coefficients the reactions are read from
/// (<c>BOOT.md</c>, Constraints: the reference reduces only when a component differs from its row's monatomic default, which is
/// the same matrix wherever the monatomic gases are present).
/// Scratch: reads <c>IndexList</c>, <c>RowActive</c>, <c>Component</c>; writes <c>Basis</c>.
/// </summary>
internal static class ReactionBasis
{
    private const int Stride = TransportSolver.Stride;

    /// <summary>Fills the basis with the columns of the set and reduces it over the component columns.</summary>
    internal static void Reduce(in StationInputs inputs, int nm)
    {
        Fill(in inputs, nm);
        Eliminate(in inputs, nm);
    }

    /// <summary>The set index of a table species, or −1 when the species is not in the set.</summary>
    internal static int LocalIndex(in StationInputs inputs, int nm, int speciesIndex)
    {
        var scratch = inputs.Scratch;
        for (var a = 0; a < nm; a++)
        {
            if (scratch.IndexList[a] == speciesIndex)
            {
                return a;
            }
        }

        return -1;
    }

    /// <summary>The stoichiometry of the species of the set, active rows only.</summary>
    private static void Fill(in StationInputs inputs, int nm)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var speciesCount = species.SpeciesCount;
        for (var i = 0; i < species.ElementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            for (var a = 0; a < nm; a++)
            {
                scratch.Basis[i * Stride + a] = species.Stoichiometry[i * speciesCount + scratch.IndexList[a]];
            }
        }
    }

    /// <summary>Gauss–Jordan over the component columns, with the entries below the cleaning threshold set to zero.</summary>
    private static void Eliminate(in StationInputs inputs, int nm)
    {
        var scratch = inputs.Scratch;
        var elementCount = inputs.Species.ElementCount;
        for (var i = 0; i < elementCount; i++)
        {
            if (scratch.RowActive[i] == 0 || scratch.Component[i] < 0)
            {
                continue;
            }

            var column = LocalIndex(in inputs, nm, scratch.Component[i]);
            if (column < 0)
            {
                continue;
            }

            var pivot = scratch.Basis[i * Stride + column];
            if (pivot == 0.0)
            {
                continue;
            }

            if (pivot != 1.0)
            {
                for (var a = 0; a < nm; a++)
                {
                    scratch.Basis[i * Stride + a] /= pivot;
                }
            }

            ClearColumn(in inputs, nm, i, column);
        }
    }

    /// <summary>Subtracts the pivot row from every other active row, so that the component column becomes a unit vector.</summary>
    private static void ClearColumn(in StationInputs inputs, int nm, int pivotRow, int column)
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

            for (var a = 0; a < nm; a++)
            {
                var value = scratch.Basis[k * Stride + a] - scratch.Basis[pivotRow * Stride + a] * factor;
                scratch.Basis[k * Stride + a] = Math.Abs(value) < TransportSolver.BasisCleaningThreshold ? 0.0 : value;
            }
        }
    }
}
