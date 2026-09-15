namespace AerospacePropellantThermodynamics.Transport;

/// <summary>
/// Stages 2 and 3 of the evaluation: the active element rows, the default species of each row (the monatomic gas of the
/// element, else the first gas of the case that holds it) and the component of each row — the gaseous species in decreasing
/// moles, each given the first free row it can serve, provided its stoichiometry column is not that of an earlier component
/// and stays independent of the rows' default species (<c>BOOT.md</c>, Constraints).
/// Scratch: reads nothing; writes <c>RowActive</c>, <c>Default</c>, <c>Component</c>, <c>RowTaken</c>, and <c>Mark</c> for
/// every species of the table: both bits cleared, then the "seen by the component search" bit set;
/// <see cref="TransportSetSelection"/> relies on the cleared "in the set" bit.
/// </summary>
internal static class TransportComponents
{
    /// <summary>Marks the active rows, gives each its default species and searches the components.</summary>
    internal static void Select(in StationInputs inputs)
    {
        MarkActiveRows(in inputs);
        var activeRows = ChooseDefaults(in inputs);
        Assign(in inputs, activeRows);
    }

    /// <summary>Whether gaseous species j is one the case can form: every element of its formula is an active row.</summary>
    internal static bool OfCase(in StationInputs inputs, int j)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var speciesCount = species.SpeciesCount;
        for (var i = 0; i < species.ElementCount; i++)
        {
            if (species.Stoichiometry[i * speciesCount + j] != 0.0 && scratch.RowActive[i] == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The atoms of one formula unit of species j, over every element row.</summary>
    private static double AtomCount(in StationInputs inputs, int j)
    {
        var species = inputs.Species;
        var sum = 0.0;
        for (var i = 0; i < species.ElementCount; i++)
        {
            sum += Math.Abs(species.Stoichiometry[i * species.SpeciesCount + j]);
        }

        return sum;
    }

    /// <summary>Whether two species have the same stoichiometry column over the active rows.</summary>
    private static bool SameColumn(in StationInputs inputs, int first, int second)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        for (var i = 0; i < species.ElementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            if (species.Stoichiometry[i * species.SpeciesCount + first] != species.Stoichiometry[i * species.SpeciesCount + second])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Clears the per-row slots and marks the rows a species with positive moles holds.</summary>
    private static void MarkActiveRows(in StationInputs inputs)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var speciesCount = species.SpeciesCount;
        var elementCount = species.ElementCount;
        for (var i = 0; i < elementCount; i++)
        {
            scratch.RowActive[i] = 0;
            scratch.Default[i] = -1;
            scratch.Component[i] = -1;
            scratch.RowTaken[i] = 0;
        }

        for (var j = 0; j < speciesCount; j++)
        {
            if (inputs.Moles[j] <= 0.0)
            {
                continue;
            }

            for (var i = 0; i < elementCount; i++)
            {
                if (species.Stoichiometry[i * speciesCount + j] != 0.0)
                {
                    scratch.RowActive[i] = 1;
                }
            }
        }
    }

    /// <summary>The default species of every active row, which is also its component until the search replaces it.</summary>
    private static int ChooseDefaults(in StationInputs inputs)
    {
        var scratch = inputs.Scratch;
        var activeRows = 0;
        for (var i = 0; i < inputs.Species.ElementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            activeRows++;
            scratch.Default[i] = DefaultOf(in inputs, i);
            scratch.Component[i] = scratch.Default[i];
        }

        return activeRows;
    }

    /// <summary>The monatomic gas of the row's element, else the first gas of the case that holds it, else −1.</summary>
    private static int DefaultOf(in StationInputs inputs, int i)
    {
        var species = inputs.Species;
        var speciesCount = species.SpeciesCount;
        for (var j = 0; j < species.GasCount; j++)
        {
            if (Math.Abs(Math.Abs(species.Stoichiometry[i * speciesCount + j]) - 1.0) < TransportSolver.UnitCountTolerance
                && Math.Abs(AtomCount(in inputs, j) - 1.0) < TransportSolver.UnitCountTolerance)
            {
                return j;
            }
        }

        for (var j = 0; j < species.GasCount; j++)
        {
            if (Math.Abs(species.Stoichiometry[i * speciesCount + j]) > TransportSolver.StoichiometryThreshold && OfCase(in inputs, j))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>The component search: the gases in decreasing moles, each offered to the rows until every active row is taken.</summary>
    private static void Assign(in StationInputs inputs, int activeRows)
    {
        var scratch = inputs.Scratch;
        for (var j = 0; j < inputs.Species.SpeciesCount; j++)
        {
            scratch.Mark[j] = 0;
        }

        var assigned = 0;
        while (assigned < activeRows)
        {
            var candidate = NextCandidate(in inputs);
            if (candidate < 0)
            {
                break;
            }

            if (TryTake(in inputs, candidate))
            {
                assigned++;
            }

            scratch.Mark[candidate] |= 1;
        }
    }

    /// <summary>The gaseous species with the most moles the search has not seen yet, or −1.</summary>
    private static int NextCandidate(in StationInputs inputs)
    {
        var scratch = inputs.Scratch;
        var candidate = -1;
        var best = 0.0;
        for (var j = 0; j < inputs.Species.GasCount; j++)
        {
            if ((scratch.Mark[j] & 1) == 0 && inputs.Moles[j] > best)
            {
                best = inputs.Moles[j];
                candidate = j;
            }
        }

        return candidate;
    }

    /// <summary>Gives the candidate the first free row it can serve, if any accepts it.</summary>
    private static bool TryTake(in StationInputs inputs, int candidate)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var speciesCount = species.SpeciesCount;
        for (var i = 0; i < species.ElementCount; i++)
        {
            if (scratch.RowActive[i] == 0 || scratch.RowTaken[i] == 1
                || species.Stoichiometry[i * speciesCount + candidate] <= TransportSolver.StoichiometryThreshold)
            {
                continue;
            }

            if (!Independent(in inputs, candidate, i))
            {
                continue;
            }

            scratch.Component[i] = candidate;
            scratch.RowTaken[i] = 1;
            return true;
        }

        return false;
    }

    /// <summary>Whether the candidate may serve the row: no earlier component has its column, and it stays independent of the defaults.</summary>
    private static bool Independent(in StationInputs inputs, int candidate, int row)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var speciesCount = species.SpeciesCount;
        var elementCount = species.ElementCount;
        var accept = true;
        for (var l = 0; l < elementCount && accept; l++)
        {
            if (scratch.RowTaken[l] == 0 || scratch.Component[l] < 0)
            {
                continue;
            }

            accept = !SameColumn(in inputs, candidate, scratch.Component[l]);
        }

        for (var k = 0; k < elementCount && accept; k++)
        {
            if (k == row || scratch.RowActive[k] == 0 || scratch.Default[k] < 0)
            {
                continue;
            }

            var other = scratch.Default[k];
            var determinant = species.Stoichiometry[row * speciesCount + candidate] * species.Stoichiometry[k * speciesCount + other]
                              - species.Stoichiometry[row * speciesCount + other] * species.Stoichiometry[k * speciesCount + candidate];
            accept = Math.Abs(determinant) > TransportSolver.StoichiometryThreshold;
        }

        return accept;
    }
}
