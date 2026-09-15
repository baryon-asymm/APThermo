using APThermo.Data;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Transport;

/// <summary>
/// The transport fits of the species of a <see cref="SpeciesTable"/>, indexed like that table and converted to SI once, at the
/// build. Host side, immutable. A gaseous species without an entry in the transport database has zero fits and is estimated
/// by the solver; condensed species never take part.
/// </summary>
internal sealed class TransportTable
{
    /// <summary>1 micropoise in Pa·s: the factor folded into the constant term of every viscosity fit.</summary>
    public const double ViscosityFactorToSi = 1e-7;

    /// <summary>1 μW/(cm·K) in W/(m·K): the factor folded into the constant term of every conductivity fit.</summary>
    public const double ConductivityFactorToSi = 1e-4;

    /// <summary>Doubles per fit in <see cref="TransportTableArrays.Fits"/>: TLow, THigh, A, B, C, D.</summary>
    public const int FitStride = 6;

    private TransportTable(SpeciesTable species, TransportTableArrays arrays, IReadOnlyList<string> withData,
                           IReadOnlyList<string> withoutData, IReadOnlyList<(string First, string Second)> pairs)
    {
        Species = species;
        Arrays = arrays;
        SpeciesWithData = withData;
        SpeciesWithoutData = withoutData;
        Pairs = pairs;
    }

    /// <summary>The species table the fits are indexed by.</summary>
    public SpeciesTable Species { get; }

    /// <summary>Gaseous species of the table with a viscosity fit, in table order.</summary>
    public IReadOnlyList<string> SpeciesWithData { get; }

    /// <summary>Gaseous species of the table without a transport entry, in table order; the solver estimates them.</summary>
    public IReadOnlyList<string> SpeciesWithoutData { get; }

    /// <summary>Pairs of gaseous species of the table with an interaction entry, in the order of the database.</summary>
    public IReadOnlyList<(string First, string Second)> Pairs { get; }

    /// <summary>The flat arrays, ready to upload.</summary>
    public TransportTableArrays Arrays { get; }

    /// <summary>The species count of the table.</summary>
    public int SpeciesCount => Species.SpeciesCount;

    /// <summary>Collects the entries of the database for the species of the table.</summary>
    public static TransportTable Build(TransportDatabase database, SpeciesTable species)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(species);
        var count = species.SpeciesCount;
        var runs = new SpeciesRuns(count);
        var fits = new List<double>();
        var pairs = new PairRuns(count);
        AppendSpeciesRuns(database, species, fits, runs);
        AppendPairRuns(database, species, fits, pairs);
        if (fits.Count == 0)
        {
            fits.AddRange(new double[FitStride]);   // a view must not be empty; the padding fit is never addressed
        }

        var arrays = new TransportTableArrays(
            viscosityStart: runs.ViscosityStart, viscosityCount: runs.ViscosityCount,
            conductivityStart: runs.ConductivityStart, conductivityCount: runs.ConductivityCount, fits: fits.ToArray(),
            pairIndex: pairs.Index, pairStart: pairs.Start.Count == 0 ? [0] : pairs.Start.ToArray(),
            pairCount: pairs.Count.Count == 0 ? [0] : pairs.Count.ToArray(),
            pairTotal: pairs.Names.Count);
        return new TransportTable(species, arrays, runs.WithData, runs.WithoutData, pairs.Names);
    }

    /// <summary>The viscosity and conductivity runs of every gaseous species of the table, in table order.</summary>
    private static void AppendSpeciesRuns(TransportDatabase database, SpeciesTable species, List<double> fits, SpeciesRuns runs)
    {
        var viscosityShift = Math.Log(ViscosityFactorToSi);
        var conductivityShift = Math.Log(ConductivityFactorToSi);
        for (var j = 0; j < species.GasCount; j++)
        {
            var name = species.Species[j];
            var entry = database.Find(name);
            if (entry is null || entry.Viscosity.Count == 0)
            {
                runs.WithoutData.Add(name);
                continue;
            }

            runs.ViscosityStart[j] = fits.Count / FitStride;
            runs.ViscosityCount[j] = Append(fits, entry.Viscosity, viscosityShift);
            runs.ConductivityStart[j] = fits.Count / FitStride;
            runs.ConductivityCount[j] = Append(fits, entry.Conductivity, conductivityShift);
            runs.WithData.Add(name);
        }
    }

    /// <summary>The interaction runs of every pair of the database whose two species are gaseous members of the table, in database order.</summary>
    private static void AppendPairRuns(TransportDatabase database, SpeciesTable species, List<double> fits, PairRuns pairs)
    {
        var viscosityShift = Math.Log(ViscosityFactorToSi);
        var count = species.SpeciesCount;
        foreach (var entry in database.Entries)
        {
            if (entry.Partner is null || entry.Viscosity.Count == 0)
            {
                continue;
            }

            var first = species.IndexOf(entry.Species);
            var second = species.IndexOf(entry.Partner);
            if (first < 0 || second < 0 || first >= species.GasCount || second >= species.GasCount || first == second
                || pairs.Index[first * count + second] >= 0)
            {
                continue;
            }

            pairs.Index[first * count + second] = pairs.Names.Count;
            pairs.Index[second * count + first] = pairs.Names.Count;
            pairs.Start.Add(fits.Count / FitStride);
            pairs.Count.Add(Append(fits, entry.Viscosity, viscosityShift));
            pairs.Names.Add((entry.Species, entry.Partner));
        }
    }

    private static int Append(List<double> fits, IReadOnlyList<TransportFit> source, double shift)
    {
        foreach (var fit in source)
        {
            fits.Add(fit.TLow);
            fits.Add(fit.THigh);
            fits.Add(fit.A);
            fits.Add(fit.B);
            fits.Add(fit.C);
            fits.Add(fit.D + shift);
        }

        return source.Count;
    }
}

/// <summary>The flat host arrays of a <see cref="TransportTable"/>. Do not modify after the build.</summary>
internal sealed class TransportTableArrays
{
    internal TransportTableArrays(int[] viscosityStart, int[] viscosityCount, int[] conductivityStart, int[] conductivityCount,
                                  double[] fits, int[] pairIndex, int[] pairStart, int[] pairCount, int pairTotal)
    {
        ViscosityStart = viscosityStart;
        ViscosityCount = viscosityCount;
        ConductivityStart = conductivityStart;
        ConductivityCount = conductivityCount;
        Fits = fits;
        PairIndex = pairIndex;
        PairStart = pairStart;
        PairCount = pairCount;
        PairTotal = pairTotal;
    }

    /// <summary>[species] first fit of the species' viscosity run; meaningful when the count is positive.</summary>
    public int[] ViscosityStart { get; }

    /// <summary>[species] viscosity fits of the species; zero for a species without data and for condensed species.</summary>
    public int[] ViscosityCount { get; }

    /// <summary>[species] first fit of the species' conductivity run.</summary>
    public int[] ConductivityStart { get; }

    /// <summary>[species] conductivity fits of the species; zero when the entry has none.</summary>
    public int[] ConductivityCount { get; }

    /// <summary>[fit * 6]: TLow, THigh, A, B, C, D with the SI factor folded into D, so that exp(A ln T + B/T + C/T² + D) is in SI.</summary>
    public double[] Fits { get; }

    /// <summary>[species * SpeciesCount + species] pair index, −1 without interaction data.</summary>
    public int[] PairIndex { get; }

    /// <summary>[pair] first fit of the pair's viscosity run (one padding entry when there is no pair).</summary>
    public int[] PairStart { get; }

    /// <summary>[pair] fits of the pair (one padding entry when there is no pair).</summary>
    public int[] PairCount { get; }

    /// <summary>The number of pairs with data.</summary>
    public int PairTotal { get; }

    /// <summary>The number of fits held.</summary>
    public int FitTotal => Fits.Length / TransportTable.FitStride;
}

/// <summary>The transport table over accelerator memory: blittable, the same layout as the arrays.</summary>
internal readonly struct TransportTableView
{
    /// <summary>The species count of the species table the fits are indexed by.</summary>
    public readonly int SpeciesCount;

    /// <summary>The number of pairs with data.</summary>
    public readonly int PairTotal;

    /// <summary>[species] first viscosity fit.</summary>
    public readonly ArrayView<int> ViscosityStart;

    /// <summary>[species] viscosity fits; zero = no data.</summary>
    public readonly ArrayView<int> ViscosityCount;

    /// <summary>[species] first conductivity fit.</summary>
    public readonly ArrayView<int> ConductivityStart;

    /// <summary>[species] conductivity fits; zero = no data.</summary>
    public readonly ArrayView<int> ConductivityCount;

    /// <summary>[fit * 6]: TLow, THigh, A, B, C, D in SI.</summary>
    public readonly ArrayView<double> Fits;

    /// <summary>[species * SpeciesCount + species] pair index, −1 without data.</summary>
    public readonly ArrayView<int> PairIndex;

    /// <summary>[pair] first fit of the pair.</summary>
    public readonly ArrayView<int> PairStart;

    /// <summary>[pair] fits of the pair.</summary>
    public readonly ArrayView<int> PairCount;

    /// <summary>Wraps views of the arrays of a table.</summary>
    public TransportTableView(int speciesCount, int pairTotal,
                              ArrayView<int> viscosityStart, ArrayView<int> viscosityCount,
                              ArrayView<int> conductivityStart, ArrayView<int> conductivityCount,
                              ArrayView<double> fits, ArrayView<int> pairIndex, ArrayView<int> pairStart, ArrayView<int> pairCount)
    {
        SpeciesCount = speciesCount;
        PairTotal = pairTotal;
        ViscosityStart = viscosityStart;
        ViscosityCount = viscosityCount;
        ConductivityStart = conductivityStart;
        ConductivityCount = conductivityCount;
        Fits = fits;
        PairIndex = pairIndex;
        PairStart = pairStart;
        PairCount = pairCount;
    }
}

/// <summary>A transport table uploaded to one accelerator; owns the buffers.</summary>
internal sealed class TransportTableBuffers : IDisposable
{
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _viscosityStart;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _viscosityCount;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _conductivityStart;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _conductivityCount;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _fits;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _pairIndex;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _pairStart;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _pairCount;

    private TransportTableBuffers(Accelerator accelerator, TransportTable table)
    {
        var arrays = table.Arrays;
        _viscosityStart = accelerator.Allocate1D(arrays.ViscosityStart);
        _viscosityCount = accelerator.Allocate1D(arrays.ViscosityCount);
        _conductivityStart = accelerator.Allocate1D(arrays.ConductivityStart);
        _conductivityCount = accelerator.Allocate1D(arrays.ConductivityCount);
        _fits = accelerator.Allocate1D(arrays.Fits);
        _pairIndex = accelerator.Allocate1D(arrays.PairIndex);
        _pairStart = accelerator.Allocate1D(arrays.PairStart);
        _pairCount = accelerator.Allocate1D(arrays.PairCount);
        Table = table;
        View = new TransportTableView(
            speciesCount: table.SpeciesCount, pairTotal: arrays.PairTotal,
            viscosityStart: _viscosityStart.View, viscosityCount: _viscosityCount.View,
            conductivityStart: _conductivityStart.View, conductivityCount: _conductivityCount.View,
            fits: _fits.View, pairIndex: _pairIndex.View, pairStart: _pairStart.View, pairCount: _pairCount.View);
    }

    /// <summary>The table the buffers hold.</summary>
    public TransportTable Table { get; }

    /// <summary>The view to pass to kernels (or to the solver on the host, on the CPU accelerator).</summary>
    public TransportTableView View { get; }

    /// <summary>Copies the table's arrays into buffers of the accelerator.</summary>
    public static TransportTableBuffers Upload(Accelerator accelerator, TransportTable table)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(table);
        return new TransportTableBuffers(accelerator, table);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _viscosityStart.Dispose();
        _viscosityCount.Dispose();
        _conductivityStart.Dispose();
        _conductivityCount.Dispose();
        _fits.Dispose();
        _pairIndex.Dispose();
        _pairStart.Dispose();
        _pairCount.Dispose();
    }
}
