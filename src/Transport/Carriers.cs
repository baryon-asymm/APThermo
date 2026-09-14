using AerospacePropellantThermodynamics.Thermo;
using ILGPU;

namespace AerospacePropellantThermodynamics.Transport;

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
