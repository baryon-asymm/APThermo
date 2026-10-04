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
    /// Doubles per case: nine per species (the seventh being rule A's tie snapshot of the gaseous logarithms, BOOT.md,
    /// the third pass of 2026-09-28, finding F1, and the eighth and ninth the two ends of the temperature bracket,
    /// 2026-10-03), the matrix, the right-hand side, the row scales, one <see cref="MaxCondensedInSolution"/>-sized
    /// slice for the tie snapshot's condensed moles, and three slices of the element count for rule A's multipliers
    /// snapshot, live coefficients and coefficients snapshot (<see cref="TieElementSlices"/>).
    /// </summary>
    public static int DoublesPerCase(int speciesCount, int elementCount)
    {
        var unknowns = MaxUnknowns(elementCount);
        return 9 * speciesCount + MaxCondensedInSolution + 3 * elementCount + unknowns * unknowns + 2 * unknowns;
    }

    /// <summary>
    /// Ints per case: the species mask, the element mask, the condensed set, and rule A's tie snapshot of the condensed
    /// set (BOOT.md, the third pass of 2026-09-28, finding F1).
    /// </summary>
    public static int IntsPerCase(int speciesCount, int elementCount) => speciesCount + elementCount + 2 * MaxCondensedInSolution;
}

/// <summary>
/// Rule A's slices of the element count (BOOT.md of the Newton child node, "The tie is per-case state"): the release
/// snapshot of the Lagrange multipliers, the live coefficients <c>c_i</c> of the tied element's combination, and the
/// snapshot of those coefficients the release takes. Grouped so that <see cref="EquilibriumScratch"/>'s constructor
/// keeps its parameter count.
/// </summary>
internal readonly struct TieElementSlices(ArrayView<double> multipliers, ArrayView<double> coefficients, ArrayView<double> coefficientSnapshot)
{
    public readonly ArrayView<double> Multipliers = multipliers;                   // [element], the release snapshot of the Lagrange multipliers
    public readonly ArrayView<double> Coefficients = coefficients;                 // [element], c_i of the live tie; zero for the tied element itself
    public readonly ArrayView<double> CoefficientSnapshot = coefficientSnapshot;   // [element], the release's snapshot of Coefficients
}

/// <summary>
/// Rule A's release snapshot (BOOT.md of the Newton child node, "Release"), grouped on 2026-10-03 so that
/// <see cref="EquilibriumScratch"/>'s constructor shrinks below its former parameter count: the snapshot of the gaseous
/// logarithms, of the condensed moles and set, and the slices of the element count.
/// </summary>
internal readonly struct TieSlices(ArrayView<double> logMoles, ArrayView<double> condensedMoles, ArrayView<int> condensedSet, TieElementSlices elements)
{
    public readonly ArrayView<double> LogMoles = logMoles;               // [species], the release snapshot of LogMoles (2026-09-28)
    public readonly ArrayView<double> CondensedMoles = condensedMoles;   // [MaxCondensedInSolution], the snapshot's condensed moles
    public readonly ArrayView<int> CondensedSet = condensedSet;          // [MaxCondensedInSolution], the snapshot's condensed set
    public readonly TieElementSlices Elements = elements;                // the multipliers snapshot, the live coefficients, their snapshot
}

/// <summary>Per-case scratch views. <see cref="Slice"/> cuts them from one double and one int view of the sizes in <see cref="ScratchLayout"/>.</summary>
internal readonly struct EquilibriumScratch(
    ArrayView<double> hOverRT, ArrayView<double> sOverR, ArrayView<double> cpOverR, ArrayView<double> gOverRT,
    ArrayView<double> logMoles, ArrayView<double> corrections, TieSlices tie,
    ArrayView<double> matrix, ArrayView<double> rightHandSide, ArrayView<double> rowScale,
    ArrayView<int> speciesActive, ArrayView<int> elementActive, ArrayView<int> condensedInSolution,
    ArrayView<double> bracketEnds)
{
    public readonly ArrayView<double> HOverRT = hOverRT;             // [species]
    public readonly ArrayView<double> SOverR = sOverR;               // [species]
    public readonly ArrayView<double> CpOverR = cpOverR;             // [species]
    public readonly ArrayView<double> GOverRT = gOverRT;             // [species]
    public readonly ArrayView<double> LogMoles = logMoles;           // [species], ln n_j of gaseous species
    public readonly ArrayView<double> Corrections = corrections;     // [species], Δln n_j of gaseous species
    public readonly TieSlices Tie = tie;                             // rule A's release snapshot, grouped (2026-10-03)
    public readonly ArrayView<double> Matrix = matrix;               // [MaxUnknowns * MaxUnknowns], row-major
    public readonly ArrayView<double> RightHandSide = rightHandSide; // [MaxUnknowns]; holds the solution after a solve
    public readonly ArrayView<double> RowScale = rowScale;           // [MaxUnknowns]
    public readonly ArrayView<int> SpeciesActive = speciesActive;    // [species], 1 when every element of the species is present
    public readonly ArrayView<int> ElementActive = elementActive;    // [element], 1 when the abundance is positive
    public readonly ArrayView<int> CondensedInSolution = condensedInSolution;    // [MaxCondensedInSolution], species indices
    public readonly ArrayView<double> BracketEnds = bracketEnds;     // [2 * species], the temperature bracket's lower then upper end, after RowScale so that no earlier slice moves

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
        var tieMultipliers = doubles.SubView(offset, elementCount);
        offset += elementCount;
        var tieCoefficients = doubles.SubView(offset, elementCount);
        offset += elementCount;
        var tieCoefficientSnapshot = doubles.SubView(offset, elementCount);
        offset += elementCount;
        var matrix = doubles.SubView(offset, unknowns * unknowns);
        offset += unknowns * unknowns;
        var rightHandSide = doubles.SubView(offset, unknowns);
        offset += unknowns;
        var rowScale = doubles.SubView(offset, unknowns);
        offset += unknowns;

        // The last slice: with no species it would be a zero-length view at the very end of the buffer, which ILGPU
        // reports as out of bounds, and a table with no species is a valid input that the solver answers with InvalidInput.
        var bracketEnds = speciesCount > 0 ? doubles.SubView(offset, 2L * speciesCount) : default;

        var speciesActive = ints.SubView(0, speciesCount);
        var elementActive = ints.SubView(speciesCount, elementCount);
        var condensed = ints.SubView(speciesCount + elementCount, ScratchLayout.MaxCondensedInSolution);
        var tieCondensedSet = ints.SubView(speciesCount + elementCount + ScratchLayout.MaxCondensedInSolution, ScratchLayout.MaxCondensedInSolution);
        var tieElements = new TieElementSlices(multipliers: tieMultipliers, coefficients: tieCoefficients, coefficientSnapshot: tieCoefficientSnapshot);
        return new EquilibriumScratch(
            hOverRT: hOverRT, sOverR: sOverR, cpOverR: cpOverR, gOverRT: gOverRT, logMoles: logMoles, corrections: corrections,
            tie: new TieSlices(logMoles: tieLogMoles, condensedMoles: tieCondensedMoles, condensedSet: tieCondensedSet, elements: tieElements),
            matrix: matrix, rightHandSide: rightHandSide, rowScale: rowScale,
            speciesActive: speciesActive, elementActive: elementActive, condensedInSolution: condensed,
            bracketEnds: bracketEnds);
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
