using APThermo.Thermo;
using ILGPU;

namespace APThermo.Transport;

/// <summary>
/// The inputs of one station, built once by <see cref="TransportSolver.Evaluate"/> and given to every stage of the evaluation,
/// so that no stage signature carries the views one by one. Blittable: views nested in a struct cost nothing in a kernel.
/// </summary>
internal readonly struct StationInputs
{
    /// <summary>The species table the moles and the transport table are indexed by.</summary>
    public readonly SpeciesTableView Species;

    /// <summary>The transport fits of the same species table.</summary>
    public readonly TransportTableView Transport;

    /// <summary>The per-case scratch; which stage reads and writes which slot is in each stage's summary.</summary>
    public readonly TransportScratch Scratch;

    /// <summary>[species] kmol per kg of the station, condensed species included.</summary>
    public readonly ArrayView<double> Moles;

    /// <summary>Station temperature, K.</summary>
    public readonly double Temperature;

    /// <summary>Wraps the inputs of one station.</summary>
    public StationInputs(in SpeciesTableView species, in TransportTableView transport, in TransportScratch scratch,
                         ArrayView<double> moles, double temperature)
    {
        Species = species;
        Transport = transport;
        Scratch = scratch;
        Moles = moles;
        Temperature = temperature;
    }
}

/// <summary>The mixture viscosity and frozen conductivity of the transport set, SI: what <see cref="MixtureRules"/> returns.</summary>
internal readonly struct MixtureTransport
{
    /// <summary>Mixture viscosity, Pa·s.</summary>
    public readonly double Viscosity;

    /// <summary>Frozen thermal conductivity, W/(m·K).</summary>
    public readonly double FrozenConductivity;

    /// <summary>Wraps the two figures of the mixture rules.</summary>
    public MixtureTransport(double viscosity, double frozenConductivity)
    {
        Viscosity = viscosity;
        FrozenConductivity = frozenConductivity;
    }
}

/// <summary>What <see cref="ReactionTerms"/> returns: the two reaction contributions of the set and the status of the solves.</summary>
internal readonly struct ReactionContribution
{
    /// <summary>Reaction heat capacity of the set, J/(kmol·K) of its gas; divided by the set's mass by <see cref="SetProperties"/>.</summary>
    public readonly double HeatCapacity;

    /// <summary>Reaction contribution to the thermal conductivity, W/(m·K).</summary>
    public readonly double Conductivity;

    /// <summary><see cref="CaseStatus.SingularMatrix"/> when a reaction system could not be solved, else <see cref="CaseStatus.Ok"/>.</summary>
    public readonly CaseStatus Status;

    /// <summary>Wraps the two contributions and the status.</summary>
    public ReactionContribution(double heatCapacity, double conductivity, CaseStatus status)
    {
        HeatCapacity = heatCapacity;
        Conductivity = conductivity;
        Status = status;
    }
}
