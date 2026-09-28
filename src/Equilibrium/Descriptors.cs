using APThermo.Thermo;
using ILGPU;

namespace APThermo.Equilibrium;

/// <summary>Which two state functions are assigned.</summary>
public enum ProblemKind
{
    /// <summary>Temperature and pressure are assigned (a tp problem).</summary>
    AssignedTemperaturePressure,

    /// <summary>Enthalpy and pressure are assigned (an hp problem).</summary>
    AssignedEnthalpyPressure,

    /// <summary>Entropy and pressure are assigned (an sp problem).</summary>
    AssignedEntropyPressure,
}

/// <summary>One case: the assigned state and the element abundances of one kilogram of mixture.</summary>
internal readonly struct EquilibriumProblem(ProblemKind kind, double pressure, double temperature, double target, ArrayView<double> elementMoles)
{
    public readonly ProblemKind Kind = kind;

    /// <summary>Pa.</summary>
    public readonly double Pressure = pressure;

    /// <summary>K for tp; the initial estimate for hp and sp (0 = the default of 3800 K).</summary>
    public readonly double Temperature = temperature;

    /// <summary>h in J/kg for hp, s in J/(kg·K) for sp, unused for tp.</summary>
    public readonly double Target = target;

    /// <summary>[element], kmol of atoms per kg of mixture (b_i°); zero marks an absent element.</summary>
    public readonly ArrayView<double> ElementMoles = elementMoles;
}

/// <summary>Sizes of the per-case scratch; the caller allocates batch-sized buffers and slices them.</summary>
internal static class ScratchLayout
{
    /// <summary>
    /// Condensed species that may be in the solution at once: <see cref="TableLimits.MaxElements"/> (2026-09-26; was
    /// 8). The phase rule never needs more than one condensed phase per element beside the gas phase, plus one more
    /// on a pinned plateau, but a table may declare up to <see cref="TableLimits.MaxElements"/> elements, and the
    /// prior 8-slot limit silently dropped stable phases beyond it into a false <c>Ok</c> (Equilibrium BOOT.md, the
    /// audit's finding 2).
    /// </summary>
    public const int MaxCondensedInSolution = TableLimits.MaxElements;

    /// <summary>Unknowns of the reduced system: elements + condensed species in the solution + ln n + ln T.</summary>
    public static int MaxUnknowns(int elementCount) => elementCount + MaxCondensedInSolution + 2;

    /// <summary>
    /// Doubles per case: seven per species (the seventh being rule A's tie snapshot of the gaseous logarithms, BOOT.md,
    /// the third pass of 2026-09-28, finding F1), the matrix, the right-hand side, the row scales, and two more
    /// <see cref="MaxCondensedInSolution"/>-sized slices for the tie snapshot's condensed moles and multipliers.
    /// </summary>
    public static int DoublesPerCase(int speciesCount, int elementCount)
    {
        var unknowns = MaxUnknowns(elementCount);
        return 7 * speciesCount + 2 * MaxCondensedInSolution + unknowns * unknowns + 2 * unknowns;
    }

    /// <summary>
    /// Ints per case: the species mask, the element mask, the condensed set, and rule A's tie snapshot of the condensed
    /// set (BOOT.md, the third pass of 2026-09-28, finding F1).
    /// </summary>
    public static int IntsPerCase(int speciesCount, int elementCount) => speciesCount + elementCount + 2 * MaxCondensedInSolution;
}

/// <summary>Per-case scratch views. <see cref="Slice"/> cuts them from one double and one int view of the sizes in <see cref="ScratchLayout"/>.</summary>
internal readonly struct EquilibriumScratch(
    ArrayView<double> hOverRT, ArrayView<double> sOverR, ArrayView<double> cpOverR, ArrayView<double> gOverRT,
    ArrayView<double> logMoles, ArrayView<double> corrections,
    ArrayView<double> tieLogMoles, ArrayView<double> tieCondensedMoles, ArrayView<double> tieMultipliers,
    ArrayView<double> matrix, ArrayView<double> rightHandSide, ArrayView<double> rowScale,
    ArrayView<int> speciesActive, ArrayView<int> elementActive, ArrayView<int> condensedInSolution, ArrayView<int> tieCondensedSet)
{
    public readonly ArrayView<double> HOverRT = hOverRT;             // [species]
    public readonly ArrayView<double> SOverR = sOverR;               // [species]
    public readonly ArrayView<double> CpOverR = cpOverR;             // [species]
    public readonly ArrayView<double> GOverRT = gOverRT;             // [species]
    public readonly ArrayView<double> LogMoles = logMoles;           // [species], ln n_j of gaseous species
    public readonly ArrayView<double> Corrections = corrections;     // [species], Δln n_j of gaseous species
    public readonly ArrayView<double> TieLogMoles = tieLogMoles;             // [species], rule A's release snapshot of LogMoles (2026-09-28)
    public readonly ArrayView<double> TieCondensedMoles = tieCondensedMoles; // [MaxCondensedInSolution], the snapshot's condensed moles
    public readonly ArrayView<double> TieMultipliers = tieMultipliers;      // [MaxCondensedInSolution], the snapshot's Lagrange multipliers ([0, elementCount))
    public readonly ArrayView<double> Matrix = matrix;               // [MaxUnknowns * MaxUnknowns], row-major
    public readonly ArrayView<double> RightHandSide = rightHandSide; // [MaxUnknowns]; holds the solution after a solve
    public readonly ArrayView<double> RowScale = rowScale;           // [MaxUnknowns]
    public readonly ArrayView<int> SpeciesActive = speciesActive;    // [species], 1 when every element of the species is present
    public readonly ArrayView<int> ElementActive = elementActive;    // [element], 1 when the abundance is positive
    public readonly ArrayView<int> CondensedInSolution = condensedInSolution;    // [MaxCondensedInSolution], species indices
    public readonly ArrayView<int> TieCondensedSet = tieCondensedSet;            // [MaxCondensedInSolution], the snapshot's condensed set

    /// <summary>Cuts the scratch of one case from views of at least <see cref="ScratchLayout.DoublesPerCase"/> and <see cref="ScratchLayout.IntsPerCase"/> elements.</summary>
    public static EquilibriumScratch Slice(ArrayView<double> doubles, ArrayView<int> ints, int speciesCount, int elementCount)
    {
        var unknowns = ScratchLayout.MaxUnknowns(elementCount);
        var offset = 0L;
        var hOverRT = doubles.SubView(offset, speciesCount);
        offset += speciesCount;
        var sOverR = doubles.SubView(offset, speciesCount);
        offset += speciesCount;
        var cpOverR = doubles.SubView(offset, speciesCount);
        offset += speciesCount;
        var gOverRT = doubles.SubView(offset, speciesCount);
        offset += speciesCount;
        var logMoles = doubles.SubView(offset, speciesCount);
        offset += speciesCount;
        var corrections = doubles.SubView(offset, speciesCount);
        offset += speciesCount;
        var tieLogMoles = doubles.SubView(offset, speciesCount);
        offset += speciesCount;
        var tieCondensedMoles = doubles.SubView(offset, ScratchLayout.MaxCondensedInSolution);
        offset += ScratchLayout.MaxCondensedInSolution;
        var tieMultipliers = doubles.SubView(offset, ScratchLayout.MaxCondensedInSolution);
        offset += ScratchLayout.MaxCondensedInSolution;
        var matrix = doubles.SubView(offset, unknowns * unknowns);
        offset += unknowns * unknowns;
        var rightHandSide = doubles.SubView(offset, unknowns);
        offset += unknowns;
        var rowScale = doubles.SubView(offset, unknowns);

        var speciesActive = ints.SubView(0, speciesCount);
        var elementActive = ints.SubView(speciesCount, elementCount);
        var condensed = ints.SubView(speciesCount + elementCount, ScratchLayout.MaxCondensedInSolution);
        var tieCondensedSet = ints.SubView(speciesCount + elementCount + ScratchLayout.MaxCondensedInSolution, ScratchLayout.MaxCondensedInSolution);
        return new EquilibriumScratch(
            hOverRT: hOverRT, sOverR: sOverR, cpOverR: cpOverR, gOverRT: gOverRT, logMoles: logMoles, corrections: corrections,
            tieLogMoles: tieLogMoles, tieCondensedMoles: tieCondensedMoles, tieMultipliers: tieMultipliers,
            matrix: matrix, rightHandSide: rightHandSide, rowScale: rowScale,
            speciesActive: speciesActive, elementActive: elementActive, condensedInSolution: condensed, tieCondensedSet: tieCondensedSet);
    }
}

/// <summary>The views a solve writes into.</summary>
internal readonly struct EquilibriumResult(ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<MixtureState> state,
                                           ArrayView<int> status, ArrayView<int> iterations)
{
    public readonly ArrayView<double> Moles = moles;               // [species], kmol per kg; zero for absent species
    public readonly ArrayView<double> Multipliers = multipliers;   // [element], the dimensionless π_i of RP-1311
    public readonly ArrayView<MixtureState> State = state;         // [1]
    public readonly ArrayView<int> Status = status;                // [1], CaseStatus
    public readonly ArrayView<int> Iterations = iterations;        // [1], Newton steps taken
}
