using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Thermo;

/// <summary>The kernel-side view of a species table: the flat host layout over accelerator memory. Blittable.</summary>
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

/// <summary>A transactional species-table upload. The ownership inventory retains buffers; the view holds their device addresses.</summary>
internal sealed class SpeciesTableBuffers : IDisposable
{
    private const int BufferColumns = 8;
    private readonly SpeciesBufferOwnership _ownership = new(BufferColumns);

    private SpeciesTableBuffers(Accelerator accelerator, SpeciesTable table)
    {
        Table = table;
        var arrays = table.Arrays;
        try
        {
            var molarMass = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.MolarMass);
            var formationEnthalpy = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.FormationEnthalpy);
            var stoichiometry = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.Stoichiometry);
            var intervalStart = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.IntervalStart);
            var intervalCount = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.IntervalCount);
            var intervalBounds = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.IntervalBounds);
            var exponents = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.Exponents);
            var coefficients = UploadColumn(_ownership, accelerator, accelerator.DefaultStream, arrays.Coefficients);
            View = new SpeciesTableView(
                speciesCount: table.SpeciesCount, gasCount: table.GasCount, elementCount: table.ElementCount,
                molarMass: molarMass.View, formationEnthalpy: formationEnthalpy.View, stoichiometry: stoichiometry.View,
                intervalStart: intervalStart.View, intervalCount: intervalCount.View,
                intervalBounds: intervalBounds.View, exponents: exponents.View, coefficients: coefficients.View);
        }
        catch (Exception primary)
        {
            _ownership.PrepareCleanup(primary);
            _ownership.Dispose();
            throw;
        }
    }

    public SpeciesTable Table { get; }

    public SpeciesTableView View { get; }

    public static SpeciesTableBuffers Upload(Accelerator accelerator, SpeciesTable table)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(table);
        return new SpeciesTableBuffers(accelerator, table);
    }

    public void Dispose() => _ownership.Dispose();

    /// <summary>Owns the allocation before a host copy can fail; the surrounding transaction handles rollback.</summary>
    private static MemoryBuffer1D<T, Stride1D.Dense> UploadColumn<T>(
        SpeciesBufferOwnership ownership, Accelerator accelerator, AcceleratorStream stream, T[] values)
        where T : unmanaged
    {
        var buffer = ownership.Acquire(() => accelerator.Allocate1D<T>(values.Length));
        if (values.Length > 0)
        {
            buffer.View.CopyFromCPU(stream, values);
        }

        return buffer;
    }
}
