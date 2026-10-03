using APThermo.Thermo;

namespace APThermo.Equilibrium.Newton;

/// <summary>
/// Rule A's read-only queries (BOOT.md, "Rule A: an element tie"): whether the row of the element whose pivot failed
/// equals a linear combination of the other active element rows over every species of the sums, the retained gases and
/// the condensed species of the solution, and the coefficients of that combination; whether a tie already found still
/// holds; whether a condensed species of the solution carries the tied element and an element of its combination; and the
/// weights and the abundance of the tie row <see cref="IterationMatrix"/> writes. A pair of elements in one ratio is the
/// case of one nonzero coefficient. The coefficients live in <see cref="TieElementSlices.Coefficients"/>. Kernel-compatible.
/// </summary>
internal static class ElementCoupling
{
    /// <summary>The relative tolerance of the species-by-species check of a combination, 1e-10 (BOOT.md, "Trigger").</summary>
    private const double CombinationTolerance = 1.0e-10;

    /// <summary>
    /// The tie of <paramref name="element"/> with a combination of the other active rows: the coefficients solve the
    /// normal equations of those rows over the species of the sums (<c>G c = g</c>, <c>G_il = Σ_j a_ij a_lj</c>,
    /// <c>g_i = Σ_j a_ij a_kj</c>) with <see cref="DenseSolver"/> in the matrix scratch, which is free during a remedy,
    /// and every species must then satisfy <c>a_kj = Σ c_i a_ij</c> to <see cref="CombinationTolerance"/>.
    /// <see cref="ElementTie.Active"/> is false when no combination holds; the coefficients are then not meaningful.
    /// </summary>
    public static ElementTie Find(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, int element)
    {
        var elementCount = table.ElementCount;
        ClearNormalEquations(scratch, elementCount);
        if (!AccumulateNormalEquations(table, scratch, result, condensedCount, element))
        {
            return default;
        }

        PinUnusedRows(scratch, element, elementCount);
        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, elementCount, elementCount))
        {
            return default;
        }

        var any = false;
        for (var i = 0; i < elementCount; i++)
        {
            var coefficient = i == element ? 0.0 : scratch.RightHandSide[i];
            scratch.TieElements.Coefficients[i] = coefficient;
            any |= coefficient != 0.0;
        }

        var tie = new ElementTie { Active = true, Element = element };
        return any && Coupled(table, scratch, result, condensedCount, tie) ? tie : default;
    }

    /// <summary>True when every species of the sums satisfies the combination of <paramref name="tie"/> within <see cref="CombinationTolerance"/>.</summary>
    public static bool Coupled(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, in ElementTie tie)
    {
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] != 0.0 && !Satisfied(table, scratch, tie.Element, j))
            {
                return false;
            }
        }

        for (var c = 0; c < condensedCount; c++)
        {
            if (!Satisfied(table, scratch, tie.Element, scratch.CondensedInSolution[c]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when a condensed species of the solution carries the tied element and an element whose coefficient is not zero.</summary>
    public static bool HeldByCondensed(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, in ElementTie tie)
    {
        var speciesCount = table.SpeciesCount;
        for (var c = 0; c < condensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            if (table.Stoichiometry[tie.Element * speciesCount + j] != 0.0 && CarriesCombinedElement(table, scratch, j))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary><c>a_kj − Σ_i c_i a_ij</c>: the weight of species <paramref name="species"/> in the tie row of element <paramref name="element"/>.</summary>
    public static double Weight(in SpeciesTableView table, in EquilibriumScratch scratch, int element, int species)
    {
        var speciesCount = table.SpeciesCount;
        var combination = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            combination += scratch.TieElements.Coefficients[i] * table.Stoichiometry[i * speciesCount + species];
        }

        return table.Stoichiometry[element * speciesCount + species] - combination;
    }

    /// <summary><c>b_k − Σ_i c_i b_i</c>: the abundance the tie row of element <paramref name="element"/> balances.</summary>
    public static double Abundance(in EquilibriumProblem problem, in EquilibriumScratch scratch, int elementCount, int element)
    {
        var combination = 0.0;
        for (var i = 0; i < elementCount; i++)
        {
            combination += scratch.TieElements.Coefficients[i] * problem.ElementMoles[i];
        }

        return problem.ElementMoles[element] - combination;
    }

    /// <summary>Whether species <paramref name="species"/> agrees with the combination: its weight is zero to the tolerance of the terms that make it up.</summary>
    private static bool Satisfied(in SpeciesTableView table, in EquilibriumScratch scratch, int element, int species)
    {
        var speciesCount = table.SpeciesCount;
        var scale = Math.Abs(table.Stoichiometry[element * speciesCount + species]);
        for (var i = 0; i < table.ElementCount; i++)
        {
            scale += Math.Abs(scratch.TieElements.Coefficients[i] * table.Stoichiometry[i * speciesCount + species]);
        }

        return Math.Abs(Weight(table, scratch, element, species)) <= CombinationTolerance * scale;
    }

    /// <summary>True when species <paramref name="species"/> carries an element whose coefficient is not zero.</summary>
    private static bool CarriesCombinedElement(in SpeciesTableView table, in EquilibriumScratch scratch, int species)
    {
        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (scratch.TieElements.Coefficients[i] != 0.0 && table.Stoichiometry[i * speciesCount + species] != 0.0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearNormalEquations(in EquilibriumScratch scratch, int elementCount)
    {
        for (var i = 0; i < elementCount; i++)
        {
            for (var l = 0; l < elementCount; l++)
            {
                scratch.Matrix[i * elementCount + l] = 0.0;
            }

            scratch.RightHandSide[i] = 0.0;
        }
    }

    /// <summary>Sums the normal equations over the retained gases and the condensed species of the solution; true when one of them carries the tied element.</summary>
    private static bool AccumulateNormalEquations(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                                  int condensedCount, int element)
    {
        var carries = false;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] != 0.0)
            {
                carries |= AddSpecies(table, scratch, j, element);
            }
        }

        for (var c = 0; c < condensedCount; c++)
        {
            carries |= AddSpecies(table, scratch, scratch.CondensedInSolution[c], element);
        }

        return carries;
    }

    /// <summary>One species' share of <c>G</c> and <c>g</c>; true when it carries the tied element.</summary>
    private static bool AddSpecies(in SpeciesTableView table, in EquilibriumScratch scratch, int species, int element)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var tied = table.Stoichiometry[element * speciesCount + species];
        for (var i = 0; i < elementCount; i++)
        {
            var ai = table.Stoichiometry[i * speciesCount + species];
            if (ai == 0.0)
            {
                continue;
            }

            for (var l = 0; l < elementCount; l++)
            {
                scratch.Matrix[i * elementCount + l] += ai * table.Stoichiometry[l * speciesCount + species];
            }

            scratch.RightHandSide[i] += ai * tied;
        }

        return tied != 0.0;
    }

    /// <summary>The tied element, an absent one and one whose row is zero on the sums take a unit row, which fixes their coefficient at zero.</summary>
    private static void PinUnusedRows(in EquilibriumScratch scratch, int element, int elementCount)
    {
        for (var i = 0; i < elementCount; i++)
        {
            if (i != element && scratch.ElementActive[i] != 0 && scratch.Matrix[i * elementCount + i] != 0.0)
            {
                continue;
            }

            for (var l = 0; l < elementCount; l++)
            {
                scratch.Matrix[i * elementCount + l] = 0.0;
                scratch.Matrix[l * elementCount + i] = 0.0;
            }

            scratch.Matrix[i * elementCount + i] = 1.0;
            scratch.RightHandSide[i] = 0.0;
        }
    }
}
