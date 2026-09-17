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
internal readonly struct EquilibriumProblem
{
    public readonly ProblemKind Kind;

    /// <summary>Pa.</summary>
    public readonly double Pressure;

    /// <summary>K for tp; the initial estimate for hp and sp (0 = the default of 3800 K).</summary>
    public readonly double Temperature;

    /// <summary>h in J/kg for hp, s in J/(kg·K) for sp, unused for tp.</summary>
    public readonly double Target;

    /// <summary>[element], kmol of atoms per kg of mixture (b_i°); zero marks an absent element.</summary>
    public readonly ArrayView<double> ElementMoles;

    public EquilibriumProblem(ProblemKind kind, double pressure, double temperature, double target, ArrayView<double> elementMoles)
    {
        Kind = kind;
        Pressure = pressure;
        Temperature = temperature;
        Target = target;
        ElementMoles = elementMoles;
    }
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
internal readonly struct EquilibriumScratch
{
    public readonly ArrayView<double> HOverRT;             // [species]
    public readonly ArrayView<double> SOverR;              // [species]
    public readonly ArrayView<double> CpOverR;             // [species]
    public readonly ArrayView<double> GOverRT;             // [species]
    public readonly ArrayView<double> LogMoles;            // [species], ln n_j of gaseous species
    public readonly ArrayView<double> Corrections;         // [species], Δln n_j of gaseous species
    public readonly ArrayView<double> Matrix;              // [MaxUnknowns * MaxUnknowns], row-major
    public readonly ArrayView<double> RightHandSide;       // [MaxUnknowns]; holds the solution after a solve
    public readonly ArrayView<double> RowScale;            // [MaxUnknowns]
    public readonly ArrayView<int> SpeciesActive;          // [species], 1 when every element of the species is present
    public readonly ArrayView<int> ElementActive;          // [element], 1 when the abundance is positive
    public readonly ArrayView<int> CondensedInSolution;    // [MaxCondensedInSolution], species indices

    public EquilibriumScratch(
        ArrayView<double> hOverRT, ArrayView<double> sOverR, ArrayView<double> cpOverR, ArrayView<double> gOverRT,
        ArrayView<double> logMoles, ArrayView<double> corrections,
        ArrayView<double> matrix, ArrayView<double> rightHandSide, ArrayView<double> rowScale,
        ArrayView<int> speciesActive, ArrayView<int> elementActive, ArrayView<int> condensedInSolution)
    {
        HOverRT = hOverRT;
        SOverR = sOverR;
        CpOverR = cpOverR;
        GOverRT = gOverRT;
        LogMoles = logMoles;
        Corrections = corrections;
        Matrix = matrix;
        RightHandSide = rightHandSide;
        RowScale = rowScale;
        SpeciesActive = speciesActive;
        ElementActive = elementActive;
        CondensedInSolution = condensedInSolution;
    }

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
internal readonly struct EquilibriumResult
{
    public readonly ArrayView<double> Moles;               // [species], kmol per kg; zero for absent species
    public readonly ArrayView<double> Multipliers;         // [element], the dimensionless π_i of RP-1311
    public readonly ArrayView<MixtureState> State;         // [1]
    public readonly ArrayView<int> Status;                 // [1], CaseStatus
    public readonly ArrayView<int> Iterations;             // [1], Newton steps taken

    public EquilibriumResult(ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<MixtureState> state,
                             ArrayView<int> status, ArrayView<int> iterations)
    {
        Moles = moles;
        Multipliers = multipliers;
        State = state;
        Status = status;
        Iterations = iterations;
    }
}
