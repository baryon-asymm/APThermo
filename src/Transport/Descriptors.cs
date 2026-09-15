using ILGPU;

namespace APThermo.Transport;

/// <summary>The transport properties of one station, SI, with the bookkeeping of the transport set the reference reports.</summary>
public struct TransportFigures
{
    /// <summary>Mixture viscosity, Pa·s.</summary>
    public double Viscosity;

    /// <summary>Frozen thermal conductivity, W/(m·K).</summary>
    public double FrozenConductivity;

    /// <summary>Frozen plus reaction thermal conductivity, W/(m·K).</summary>
    public double ReactingConductivity;

    /// <summary>Cp_fr η / λ_fr over the transport set.</summary>
    public double FrozenPrandtl;

    /// <summary>Cp_eq η / λ_eq over the transport set.</summary>
    public double ReactingPrandtl;

    /// <summary>Frozen heat capacity of the transport set, J per kg of that gas per K (the reference's cp_fr when transport is on).</summary>
    public double FrozenHeatCapacity;

    /// <summary>Frozen plus reaction heat capacity of the transport set, J/(kg·K) on the same basis.</summary>
    public double EquilibriumHeatCapacity;

    /// <summary>The mole fraction of the transport set carried by species without a transport entry, whose properties are estimated.</summary>
    public double EstimatedMoleFraction;

    /// <summary>Species in the transport set (the reference's NM).</summary>
    public int SpeciesCount;

    /// <summary>Independent reactions among them after the trace eliminations (the reference's NR).</summary>
    public int ReactionCount;

    /// <summary>Species of the set without a transport entry.</summary>
    public int EstimatedSpeciesCount;

    /// <summary>Species of the set below <see cref="TransportSolver.TraceFraction"/> removed from the reaction set.</summary>
    public int TraceEliminations;

    /// <summary>1 when a species was refused because the set had reached <see cref="TransportLayout.MaxSpecies"/>.</summary>
    public int Capped;
}

/// <summary>Sizes of the per-case scratch of the transport solver.</summary>
public static class TransportLayout
{
    /// <summary>The largest transport set: the reference's limit on the species taking part.</summary>
    public const int MaxSpecies = 40;

    /// <summary>Doubles per case: four MaxSpecies² matrices, the reduced basis (elements × MaxSpecies) and eight vectors.</summary>
    public static int DoublesPerCase(int speciesCount, int elementCount) =>
        4 * MaxSpecies * MaxSpecies + elementCount * MaxSpecies + 8 * MaxSpecies;

    /// <summary>Ints per case: a mark per species, four vectors of MaxSpecies and four of the element count.</summary>
    public static int IntsPerCase(int speciesCount, int elementCount) =>
        speciesCount + 4 * MaxSpecies + 4 * elementCount;
}

/// <summary>Slices of batch-sized buffers for one case of the transport solver, sized by <see cref="TransportLayout"/>.</summary>
public readonly struct TransportScratch
{
    /// <summary>[MaxSpecies²] η_ij of the set, row-major with stride MaxSpecies; the diagonal holds the pure viscosities.</summary>
    public readonly ArrayView<double> Eta;

    /// <summary>[MaxSpecies²] reaction coefficients α_rj, stride MaxSpecies.</summary>
    public readonly ArrayView<double> Alpha;

    /// <summary>[MaxSpecies²] the matrix of the reaction heat capacity, stride MaxSpecies.</summary>
    public readonly ArrayView<double> Matrix;

    /// <summary>[MaxSpecies²] the matrix of the reaction conductivity, stride MaxSpecies.</summary>
    public readonly ArrayView<double> MatrixReacting;

    /// <summary>[elements × MaxSpecies] the component basis reduced over the columns of the set, stride MaxSpecies.</summary>
    public readonly ArrayView<double> Basis;

    /// <summary>[MaxSpecies] pure conductivities.</summary>
    public readonly ArrayView<double> Cond;

    /// <summary>[MaxSpecies] mole fractions within the set.</summary>
    public readonly ArrayView<double> Xs;

    /// <summary>[MaxSpecies] Cp°/R of the set.</summary>
    public readonly ArrayView<double> Cp;

    /// <summary>[MaxSpecies] H°/RT of the set.</summary>
    public readonly ArrayView<double> H;

    /// <summary>[MaxSpecies] reaction enthalpies ΔH/RT.</summary>
    public readonly ArrayView<double> DeltaH;

    /// <summary>[MaxSpecies] right-hand side, then the solution.</summary>
    public readonly ArrayView<double> Rhs;

    /// <summary>[MaxSpecies] row scales of the dense solver.</summary>
    public readonly ArrayView<double> RowScale;

    /// <summary>[MaxSpecies] a working vector.</summary>
    public readonly ArrayView<double> Stx;

    /// <summary>[species] marks: bit 1 = seen by the component search, bit 2 = in the set.</summary>
    public readonly ArrayView<int> Mark;

    /// <summary>[MaxSpecies] table indices of the set.</summary>
    public readonly ArrayView<int> IndexList;

    /// <summary>[MaxSpecies] set indices of the components.</summary>
    public readonly ArrayView<int> CompLocal;

    /// <summary>[MaxSpecies] element rows of the components.</summary>
    public readonly ArrayView<int> CompRow;

    /// <summary>[MaxSpecies] 1 for a component of the set.</summary>
    public readonly ArrayView<int> IsComponent;

    /// <summary>[elements] the component species of each element row, −1 for none.</summary>
    public readonly ArrayView<int> Component;

    /// <summary>[elements] the default species of each row: the monatomic gas, else the first gas containing the element.</summary>
    public readonly ArrayView<int> Default;

    /// <summary>[elements] 1 once the component search assigned the row.</summary>
    public readonly ArrayView<int> RowTaken;

    /// <summary>[elements] 1 when a species with positive moles contains the element.</summary>
    public readonly ArrayView<int> RowActive;

    /// <summary>Wraps the given slices.</summary>
    public TransportScratch(ArrayView<double> eta, ArrayView<double> alpha, ArrayView<double> matrix, ArrayView<double> matrixReacting,
                            ArrayView<double> basis, ArrayView<double> cond, ArrayView<double> xs, ArrayView<double> cp, ArrayView<double> h,
                            ArrayView<double> deltaH, ArrayView<double> rhs, ArrayView<double> rowScale, ArrayView<double> stx,
                            ArrayView<int> mark, ArrayView<int> indexList, ArrayView<int> compLocal, ArrayView<int> compRow,
                            ArrayView<int> isComponent, ArrayView<int> component, ArrayView<int> @default, ArrayView<int> rowTaken,
                            ArrayView<int> rowActive)
    {
        Eta = eta;
        Alpha = alpha;
        Matrix = matrix;
        MatrixReacting = matrixReacting;
        Basis = basis;
        Cond = cond;
        Xs = xs;
        Cp = cp;
        H = h;
        DeltaH = deltaH;
        Rhs = rhs;
        RowScale = rowScale;
        Stx = stx;
        Mark = mark;
        IndexList = indexList;
        CompLocal = compLocal;
        CompRow = compRow;
        IsComponent = isComponent;
        Component = component;
        Default = @default;
        RowTaken = rowTaken;
        RowActive = rowActive;
    }

    /// <summary>Cuts one case's scratch from views of at least <see cref="TransportLayout.DoublesPerCase"/> and <see cref="TransportLayout.IntsPerCase"/> elements.</summary>
    public static TransportScratch Slice(ArrayView<double> doubles, ArrayView<int> ints, int speciesCount, int elementCount)
    {
        var m = TransportLayout.MaxSpecies;
        var square = m * m;
        var offset = 0L;
        var eta = doubles.SubView(offset, square);
        offset += square;
        var alpha = doubles.SubView(offset, square);
        offset += square;
        var matrix = doubles.SubView(offset, square);
        offset += square;
        var matrixReacting = doubles.SubView(offset, square);
        offset += square;
        var basis = doubles.SubView(offset, elementCount * m);
        offset += elementCount * m;
        var cond = doubles.SubView(offset, m);
        offset += m;
        var xs = doubles.SubView(offset, m);
        offset += m;
        var cp = doubles.SubView(offset, m);
        offset += m;
        var h = doubles.SubView(offset, m);
        offset += m;
        var deltaH = doubles.SubView(offset, m);
        offset += m;
        var rhs = doubles.SubView(offset, m);
        offset += m;
        var rowScale = doubles.SubView(offset, m);
        offset += m;
        var stx = doubles.SubView(offset, m);

        var intOffset = 0L;
        var mark = ints.SubView(intOffset, speciesCount);
        intOffset += speciesCount;
        var indexList = ints.SubView(intOffset, m);
        intOffset += m;
        var compLocal = ints.SubView(intOffset, m);
        intOffset += m;
        var compRow = ints.SubView(intOffset, m);
        intOffset += m;
        var isComponent = ints.SubView(intOffset, m);
        intOffset += m;
        var component = ints.SubView(intOffset, elementCount);
        intOffset += elementCount;
        var @default = ints.SubView(intOffset, elementCount);
        intOffset += elementCount;
        var rowTaken = ints.SubView(intOffset, elementCount);
        intOffset += elementCount;
        var rowActive = ints.SubView(intOffset, elementCount);

        return new TransportScratch(
            eta: eta, alpha: alpha, matrix: matrix, matrixReacting: matrixReacting,
            basis: basis, cond: cond, xs: xs, cp: cp, h: h,
            deltaH: deltaH, rhs: rhs, rowScale: rowScale, stx: stx,
            mark: mark, indexList: indexList, compLocal: compLocal, compRow: compRow,
            isComponent: isComponent, component: component, @default: @default, rowTaken: rowTaken,
            rowActive: rowActive);
    }
}
