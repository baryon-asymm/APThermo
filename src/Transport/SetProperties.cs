using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Transport;

/// <summary>
/// Stage 13 of the evaluation: the mass and the heat capacities of one kilogram of the set's gas, the two Prandtl numbers, and
/// the transport figures of the station.
/// Scratch: reads <c>IndexList</c>, <c>Xs</c>, <c>Cp</c>; writes nothing.
/// </summary>
internal static class SetProperties
{
    /// <summary>
    /// Writes the viscosity, both conductivities, both heat capacities and both Prandtl numbers of the station into
    /// <paramref name="figures"/>; the bookkeeping fields belong to the stages that count them.
    /// </summary>
    internal static void Fill(in StationInputs inputs, int nm, in MixtureTransport mixture,
                              double reactionHeatCapacity, double reactionConductivity, ref TransportFigures figures)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var massOfSet = 0.0;
        var cpOfSet = 0.0;
        for (var a = 0; a < nm; a++)
        {
            massOfSet += scratch.Xs[a] * species.MolarMass[scratch.IndexList[a]];
            cpOfSet += scratch.Xs[a] * scratch.Cp[a];
        }

        var frozenHeatCapacity = PhysicalConstants.R * cpOfSet / massOfSet;
        var equilibriumHeatCapacity = frozenHeatCapacity + reactionHeatCapacity / massOfSet;
        var reactingConductivity = mixture.FrozenConductivity + reactionConductivity;
        figures.Viscosity = mixture.Viscosity;
        figures.FrozenConductivity = mixture.FrozenConductivity;
        figures.ReactingConductivity = reactingConductivity;
        figures.FrozenPrandtl = mixture.Viscosity * frozenHeatCapacity / mixture.FrozenConductivity;
        figures.ReactingPrandtl = mixture.Viscosity * equilibriumHeatCapacity / reactingConductivity;
        figures.FrozenHeatCapacity = frozenHeatCapacity;
        figures.EquilibriumHeatCapacity = equilibriumHeatCapacity;
    }
}
