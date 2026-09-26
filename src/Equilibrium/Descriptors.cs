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
    /// <summary>Condensed species that may be in the solution at once.</summary>
    public const int MaxCondensedInSolution = 8;

    /// <summary>Unknowns of the reduced system: elements + condensed species in the solution + ln n + ln T.</summary>
    public static int MaxUnknowns(int elementCount) => elementCount + MaxCondensedInSolution + 2;

    /// <summary>Doubles per case: six per species, the matrix, the right-hand side and the row scales.</summary>
    public static int DoublesPerCase(int speciesCount, int elementCount)
    {
        var unknowns = MaxUnknowns(elementCount);
        return 6 * speciesCount + unknowns * unknowns + 2 * unknowns;
    }

    /// <summary>Ints per case: the species mask, the element mask and the condensed set.</summary>
    public static int IntsPerCase(int speciesCount, int elementCount) => speciesCount + elementCount + MaxCondensedInSolution;
}

/// <summary>Per-case scratch views. <see cref="Slice"/> cuts them from one double and one int view of the sizes in <see cref="ScratchLayout"/>.</summary>
internal readonly struct EquilibriumScratch(
    ArrayView<double> hOverRT, ArrayView<double> sOverR, ArrayView<double> cpOverR, ArrayView<double> gOverRT,
    ArrayView<double> logMoles, ArrayView<double> corrections,
    ArrayView<double> matrix, ArrayView<double> rightHandSide, ArrayView<double> rowScale,
    ArrayView<int> speciesActive, ArrayView<int> elementActive, ArrayView<int> condensedInSolution)
{
    public readonly ArrayView<double> HOverRT = hOverRT;             // [species]
    public readonly ArrayView<double> SOverR = sOverR;               // [species]
    public readonly ArrayView<double> CpOverR = cpOverR;             // [species]
    public readonly ArrayView<double> GOverRT = gOverRT;             // [species]
    public readonly ArrayView<double> LogMoles = logMoles;           // [species], ln n_j of gaseous species
    public readonly ArrayView<double> Corrections = corrections;     // [species], Δln n_j of gaseous species
    public readonly ArrayView<double> Matrix = matrix;               // [MaxUnknowns * MaxUnknowns], row-major
    public readonly ArrayView<double> RightHandSide = rightHandSide; // [MaxUnknowns]; holds the solution after a solve
    public readonly ArrayView<double> RowScale = rowScale;           // [MaxUnknowns]
    public readonly ArrayView<int> SpeciesActive = speciesActive;    // [species], 1 when every element of the species is present
    public readonly ArrayView<int> ElementActive = elementActive;    // [element], 1 when the abundance is positive
    public readonly ArrayView<int> CondensedInSolution = condensedInSolution;    // [MaxCondensedInSolution], species indices

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
        var matrix = doubles.SubView(offset, unknowns * unknowns);
        offset += unknowns * unknowns;
        var rightHandSide = doubles.SubView(offset, unknowns);
        offset += unknowns;
        var rowScale = doubles.SubView(offset, unknowns);

        var speciesActive = ints.SubView(0, speciesCount);
        var elementActive = ints.SubView(speciesCount, elementCount);
        var condensed = ints.SubView(speciesCount + elementCount, ScratchLayout.MaxCondensedInSolution);
        return new EquilibriumScratch(
            hOverRT: hOverRT, sOverR: sOverR, cpOverR: cpOverR, gOverRT: gOverRT, logMoles: logMoles, corrections: corrections,
            matrix: matrix, rightHandSide: rightHandSide, rowScale: rowScale,
            speciesActive: speciesActive, elementActive: elementActive, condensedInSolution: condensed);
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
