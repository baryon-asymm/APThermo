using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Thermo;

/// <summary>The kernel-side view of a species table: the same layout as <see cref="SpeciesTableArrays"/>, over accelerator memory. Blittable.</summary>
internal readonly struct SpeciesTableView(
    int speciesCount, int gasCount, int elementCount,
    ArrayView<double> molarMass, ArrayView<double> formationEnthalpy, ArrayView<double> stoichiometry,
    ArrayView<int> intervalStart, ArrayView<int> intervalCount,
    ArrayView<double> intervalBounds, ArrayView<double> exponents, ArrayView<double> coefficients)
{
    public readonly int SpeciesCount = speciesCount;

    public readonly int GasCount = gasCount;

    public readonly int ElementCount = elementCount;

    public readonly ArrayView<double> MolarMass = molarMass;

    public readonly ArrayView<double> FormationEnthalpy = formationEnthalpy;

    public readonly ArrayView<double> Stoichiometry = stoichiometry;

    public readonly ArrayView<int> IntervalStart = intervalStart;

    public readonly ArrayView<int> IntervalCount = intervalCount;

    public readonly ArrayView<double> IntervalBounds = intervalBounds;

    public readonly ArrayView<double> Exponents = exponents;

    public readonly ArrayView<double> Coefficients = coefficients;
}

/// <summary>
/// A species table uploaded to an accelerator: owns the buffers and exposes the view. On the CPU accelerator the view is
/// also readable from host code, which is how tests and the host path evaluate the species functions.
/// </summary>
internal sealed class SpeciesTableBuffers : IDisposable
{
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _molarMass;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _formationEnthalpy;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _stoichiometry;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _intervalStart;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _intervalCount;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _intervalBounds;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _exponents;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _coefficients;

    private SpeciesTableBuffers(Accelerator accelerator, SpeciesTable table)
    {
        var arrays = table.Arrays;
        _molarMass = accelerator.Allocate1D(arrays.MolarMass);
        _formationEnthalpy = accelerator.Allocate1D(arrays.FormationEnthalpy);
        _stoichiometry = accelerator.Allocate1D(arrays.Stoichiometry);
        _intervalStart = accelerator.Allocate1D(arrays.IntervalStart);
        _intervalCount = accelerator.Allocate1D(arrays.IntervalCount);
        _intervalBounds = accelerator.Allocate1D(arrays.IntervalBounds);
        _exponents = accelerator.Allocate1D(arrays.Exponents);
        _coefficients = accelerator.Allocate1D(arrays.Coefficients);
        Table = table;
        View = new SpeciesTableView(
            speciesCount: table.SpeciesCount, gasCount: table.GasCount, elementCount: table.ElementCount,
            molarMass: _molarMass.View, formationEnthalpy: _formationEnthalpy.View, stoichiometry: _stoichiometry.View,
            intervalStart: _intervalStart.View, intervalCount: _intervalCount.View,
            intervalBounds: _intervalBounds.View, exponents: _exponents.View, coefficients: _coefficients.View);
    }

    /// <summary>The table the buffers hold.</summary>
    public SpeciesTable Table { get; }

    /// <summary>The view to pass to kernels (or to the species functions on the host, on the CPU accelerator).</summary>
    public SpeciesTableView View { get; }

    /// <summary>Copies the table's arrays into buffers of the accelerator.</summary>
    public static SpeciesTableBuffers Upload(Accelerator accelerator, SpeciesTable table)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(table);
        return new SpeciesTableBuffers(accelerator, table);
    }

    public void Dispose()
    {
        _molarMass.Dispose();
        _formationEnthalpy.Dispose();
        _stoichiometry.Dispose();
        _intervalStart.Dispose();
        _intervalCount.Dispose();
        _intervalBounds.Dispose();
        _exponents.Dispose();
        _coefficients.Dispose();
    }
}
