using APThermo.Execution;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Problems;

/// <summary>
/// One station of a rocket result, or the state of an equilibrium result; compositions by name, without a
/// threshold. Nominal, with an internal constructor (root <c>BOOT.md</c>, Delivery: Tree contracts, "records
/// the library creates for consumers ... have internal constructors"): no consumer builds one, only reads it.
/// Every property is <c>init</c> so that this node's own tests can build a comparison copy with <c>with</c>.
/// </summary>
public sealed record Station
{
    internal Station(
        string name, MixtureState state, PerformanceFigures? performance,
        IReadOnlyDictionary<string, double> moleFractions, IReadOnlyDictionary<string, double> condensedMassFractions,
        TransportFigures? transport, CaseStatus? transportStatus, CaseStatus status)
    {
        Name = name;
        State = state;
        Performance = performance;
        MoleFractions = moleFractions;
        CondensedMassFractions = condensedMassFractions;
        Transport = transport;
        TransportStatus = transportStatus;
        Status = status;
    }

    /// <summary>"chamber", "throat", "exit1", "exit2", … ; "state" for an equilibrium result.</summary>
    public string Name { get; init; }

    /// <summary>Zero where the status is not Ok.</summary>
    public MixtureState State { get; init; }

    /// <summary>Rocket stations only.</summary>
    public PerformanceFigures? Performance { get; init; }

    /// <summary>Every species of the table, n_j over the moles of all species, as the reference reports them.</summary>
    public IReadOnlyDictionary<string, double> MoleFractions { get; init; }

    /// <summary>Every condensed species of the table, n_j M_j.</summary>
    public IReadOnlyDictionary<string, double> CondensedMassFractions { get; init; }

    /// <summary>Null when not requested or not Ok; see <see cref="TransportStatus"/>.</summary>
    public TransportFigures? Transport { get; init; }

    /// <summary>Null when transport was not requested.</summary>
    public CaseStatus? TransportStatus { get; init; }

    public CaseStatus Status { get; init; }
}

/// <summary>
/// What one rocket case produced. Nominal, with an internal constructor (root <c>BOOT.md</c>, Delivery: Tree
/// contracts): only this node builds one.
/// </summary>
public sealed record RocketResult
{
    internal RocketResult(
        Propellant? propellant, ElementalMixture mixture, double mixtureMass, RocketProblem problem,
        double? oxidizerToFuelRatio, IReadOnlyList<string> species, IReadOnlyList<Station> stations,
        CaseStatus status, AcceleratorInfo accelerator)
    {
        Propellant = propellant;
        Mixture = mixture;
        MixtureMass = mixtureMass;
        Problem = problem;
        OxidizerToFuelRatio = oxidizerToFuelRatio;
        Species = species;
        Stations = stations;
        Status = status;
        Accelerator = accelerator;
    }

    /// <summary>Null for an elemental mixture.</summary>
    public Propellant? Propellant { get; }

    /// <summary>The element moles and enthalpy the case started from.</summary>
    public ElementalMixture Mixture { get; }

    /// <summary>kg: Σ n_i A_i of those element moles with the database's atomic weights; one within the mixture's MassTolerance.</summary>
    public double MixtureMass { get; }

    public RocketProblem Problem { get; }

    /// <summary>The ratio of the mixture rule, or null.</summary>
    public double? OxidizerToFuelRatio { get; }

    /// <summary>Table order: gases, then condensed species.</summary>
    public IReadOnlyList<string> Species { get; }

    /// <summary>Chamber, throat, exits in order.</summary>
    public IReadOnlyList<Station> Stations { get; }

    public CaseStatus Status { get; }

    public AcceleratorInfo Accelerator { get; }
}

/// <summary>
/// What one equilibrium case produced. Nominal, with an internal constructor (root <c>BOOT.md</c>, Delivery:
/// Tree contracts): only this node builds one.
/// </summary>
public sealed record EquilibriumResult
{
    internal EquilibriumResult(
        Propellant? propellant, ElementalMixture mixture, double mixtureMass, EquilibriumProblem problem,
        IReadOnlyList<string> species, Station state, CaseStatus status, AcceleratorInfo accelerator)
    {
        Propellant = propellant;
        Mixture = mixture;
        MixtureMass = mixtureMass;
        Problem = problem;
        Species = species;
        State = state;
        Status = status;
        Accelerator = accelerator;
    }

    public Propellant? Propellant { get; }

    public ElementalMixture Mixture { get; }

    /// <summary>kg, as on <see cref="RocketResult.MixtureMass"/>.</summary>
    public double MixtureMass { get; }

    public EquilibriumProblem Problem { get; }

    public IReadOnlyList<string> Species { get; }

    public Station State { get; }

    public CaseStatus Status { get; }

    public AcceleratorInfo Accelerator { get; }
}
