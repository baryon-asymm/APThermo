using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Execution;

/// <summary>A batch of equilibrium cases, structure of arrays, one entry per case; the element order is the table's.</summary>
public sealed class EquilibriumBatch
{
    public EquilibriumBatch(int count, int elementCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementCount);
        ElementCount = elementCount;
        Kind = new ProblemKind[count];
        Pressure = new double[count];
        Temperature = new double[count];
        Target = new double[count];
        ElementMoles = new double[count * elementCount];
    }

    public int Count => Kind.Length;

    public int ElementCount { get; }

    public ProblemKind[] Kind { get; }

    /// <summary>[case] Pa.</summary>
    public double[] Pressure { get; }

    /// <summary>[case] K for tp, the initial estimate for hp and sp (0 = the solver's default).</summary>
    public double[] Temperature { get; }

    /// <summary>[case] h in J/kg for hp, s in J/(kg·K) for sp, unused for tp.</summary>
    public double[] Target { get; }

    /// <summary>[case * ElementCount + element] kmol per kg.</summary>
    public double[] ElementMoles { get; }

    internal void Validate(int elementCount)
    {
        if (elementCount != ElementCount)
        {
            throw new ArgumentException($"the batch has {ElementCount} elements per case, the table {elementCount}");
        }

        if (Pressure.Length != Count || Temperature.Length != Count || Target.Length != Count || ElementMoles.Length != Count * ElementCount)
        {
            throw new ArgumentException("the batch arrays have inconsistent lengths");
        }
    }
}

/// <summary>A batch of rocket cases: every case has the same exit specification kinds, in station order.</summary>
public sealed class RocketBatch
{
    public RocketBatch(int count, int elementCount, ExitSpecification[] exitKinds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementCount);
        ArgumentNullException.ThrowIfNull(exitKinds);
        ElementCount = elementCount;
        ExitKinds = (ExitSpecification[])exitKinds.Clone();
        ChamberPressure = new double[count];
        ReactantEnthalpy = new double[count];
        TemperatureEstimate = new double[count];
        Flow = new FlowModel[count];
        ElementMoles = new double[count * elementCount];
        ExitValues = new double[count * exitKinds.Length];
    }

    public int Count => ChamberPressure.Length;

    public int ElementCount { get; }

    public int Exits => ExitKinds.Length;

    public int StationCount => RocketLayout.StationCount(Exits);

    /// <summary>[case] Pa.</summary>
    public double[] ChamberPressure { get; }

    /// <summary>[case] J/kg of propellant.</summary>
    public double[] ReactantEnthalpy { get; }

    /// <summary>[case] K, 0 = the solver's default.</summary>
    public double[] TemperatureEstimate { get; }

    public FlowModel[] Flow { get; }

    /// <summary>[case * ElementCount + element] kmol per kg.</summary>
    public double[] ElementMoles { get; }

    /// <summary>[case * Exits + exit] area ratio or p_c/p_e, by <see cref="ExitKinds"/>.</summary>
    public double[] ExitValues { get; }

    /// <summary>[exit] the same for every case.</summary>
    public ExitSpecification[] ExitKinds { get; }

    internal void Validate(int elementCount)
    {
        if (elementCount != ElementCount)
        {
            throw new ArgumentException($"the batch has {ElementCount} elements per case, the table {elementCount}");
        }

        if (ReactantEnthalpy.Length != Count || TemperatureEstimate.Length != Count || Flow.Length != Count
            || ElementMoles.Length != Count * ElementCount || ExitValues.Length != Count * Exits)
        {
            throw new ArgumentException("the batch arrays have inconsistent lengths");
        }
    }
}

/// <summary>A batch of stations for the transport pass: a temperature and a composition each.</summary>
public sealed class TransportBatch
{
    public TransportBatch(int stationCount, int speciesCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stationCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speciesCount);
        SpeciesCount = speciesCount;
        Temperature = new double[stationCount];
        Moles = new double[stationCount * speciesCount];
    }

    private TransportBatch(double[] temperature, double[] moles, int speciesCount)
    {
        Temperature = temperature;
        Moles = moles;
        SpeciesCount = speciesCount;
    }

    public int Count => Temperature.Length;

    public int SpeciesCount { get; }

    /// <summary>[station] K.</summary>
    public double[] Temperature { get; }

    /// <summary>[station * SpeciesCount + species] kmol per kg.</summary>
    public double[] Moles { get; }

    /// <summary>Every station of a rocket batch result, in its order; the moles array is shared, not copied.</summary>
    public static TransportBatch FromRocket(RocketBatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new TransportBatch(result.Stations.Select(s => s.Temperature).ToArray(), result.Moles, result.SpeciesCount);
    }

    /// <summary>Every case of an equilibrium batch result; the moles array is shared, not copied.</summary>
    public static TransportBatch FromEquilibrium(EquilibriumBatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new TransportBatch(result.State.Select(s => s.Temperature).ToArray(), result.Moles, result.SpeciesCount);
    }

    internal void Validate(int speciesCount)
    {
        if (speciesCount != SpeciesCount)
        {
            throw new ArgumentException($"the batch has {SpeciesCount} species per station, the table {speciesCount}");
        }

        if (Moles.Length != Count * SpeciesCount)
        {
            throw new ArgumentException("the batch arrays have inconsistent lengths");
        }
    }
}

/// <summary>What an equilibrium batch produced.</summary>
public sealed class EquilibriumBatchResult
{
    internal EquilibriumBatchResult(int speciesCount, MixtureState[] state, double[] moles, CaseStatus[] status, int[] iterations,
                                    RunTimings timings, AcceleratorInfo accelerator)
    {
        SpeciesCount = speciesCount;
        State = state;
        Moles = moles;
        Status = status;
        Iterations = iterations;
        Timings = timings;
        Accelerator = accelerator;
    }

    public int Count => State.Length;

    public int SpeciesCount { get; }

    /// <summary>[case]; not written for a case whose status is not Ok (zero).</summary>
    public MixtureState[] State { get; }

    /// <summary>[case * SpeciesCount + species] kmol per kg.</summary>
    public double[] Moles { get; }

    public CaseStatus[] Status { get; }

    public int[] Iterations { get; }

    public RunTimings Timings { get; }

    public AcceleratorInfo Accelerator { get; }
}

/// <summary>What a rocket batch produced; stations are chamber, throat, then the exits, per case.</summary>
public sealed class RocketBatchResult
{
    internal RocketBatchResult(int speciesCount, int stationCount, MixtureState[] stations, double[] moles, PerformanceFigures[] figures,
                               CaseStatus[] stationStatus, int[] iterations, CaseStatus[] status, RunTimings timings, AcceleratorInfo accelerator)
    {
        SpeciesCount = speciesCount;
        StationCount = stationCount;
        Stations = stations;
        Moles = moles;
        Figures = figures;
        StationStatus = stationStatus;
        Iterations = iterations;
        Status = status;
        Timings = timings;
        Accelerator = accelerator;
    }

    public int Count => Status.Length;

    public int SpeciesCount { get; }

    /// <summary>Stations per case: 2 + exits.</summary>
    public int StationCount { get; }

    /// <summary>[case * StationCount + station].</summary>
    public MixtureState[] Stations { get; }

    /// <summary>[(case * StationCount + station) * SpeciesCount + species] kmol per kg.</summary>
    public double[] Moles { get; }

    /// <summary>[case * StationCount + station].</summary>
    public PerformanceFigures[] Figures { get; }

    /// <summary>[case * StationCount + station].</summary>
    public CaseStatus[] StationStatus { get; }

    /// <summary>[case * StationCount + station] equilibrium iterations of the last solve at the station.</summary>
    public int[] Iterations { get; }

    /// <summary>[case].</summary>
    public CaseStatus[] Status { get; }

    public RunTimings Timings { get; }

    public AcceleratorInfo Accelerator { get; }
}

/// <summary>What a transport batch produced, one entry per station of the batch.</summary>
public sealed class TransportBatchResult
{
    internal TransportBatchResult(TransportFigures[] figures, CaseStatus[] status, RunTimings timings, AcceleratorInfo accelerator)
    {
        Figures = figures;
        Status = status;
        Timings = timings;
        Accelerator = accelerator;
    }

    public int Count => Status.Length;

    public TransportFigures[] Figures { get; }

    public CaseStatus[] Status { get; }

    public RunTimings Timings { get; }

    public AcceleratorInfo Accelerator { get; }
}
