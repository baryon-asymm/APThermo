using APThermo.Thermo;

namespace APThermo.Transport;

/// <summary>
/// Stages 6 and 10 of the evaluation, run at those two places of the order: the per-species and per-pair data of the transport
/// set. <see cref="Fits"/> takes what the transport table holds, <see cref="Estimates"/> fills what it does not — hard spheres
/// with the reference's collision integral and the modified Eucken relation for a species without an entry, equation (5.5) for
/// a pair without one (<c>BOOT.md</c>, Constraints).
/// Scratch: reads <c>IndexList</c>; writes <c>Xs</c>, <c>Cp</c>, <c>H</c>, <c>Cond</c> and <c>Eta</c> (the diagonal holds the
/// pure viscosities, the off-diagonal the interaction ones).
/// </summary>
internal static class SetSpeciesProperties
{
    private const int Stride = TransportSolver.Stride;

    /// <summary>
    /// The mole fraction within the set, the species functions and the fitted viscosities and conductivities of the
    /// <paramref name="nm"/> species of the set, whose moles sum to <paramref name="total"/>; a species or a pair without data
    /// is left at zero for <see cref="Estimates"/>. Counts the species without a viscosity entry into the figures.
    /// </summary>
    internal static void Fits(in StationInputs inputs, int nm, double total, ref TransportFigures figures)
    {
        var species = inputs.Species;
        var transport = inputs.Transport;
        var scratch = inputs.Scratch;
        var temperature = inputs.Temperature;
        var speciesCount = species.SpeciesCount;
        var estimatedCount = 0;
        var estimatedFraction = 0.0;
        for (var a = 0; a < nm; a++)
        {
            var j = scratch.IndexList[a];
            scratch.Xs[a] = inputs.Moles[j] / total;
            scratch.Cp[a] = SpeciesFunctions.CpOverR(in species, j, temperature);
            scratch.H[a] = SpeciesFunctions.HOverRT(in species, j, temperature);
            scratch.Cond[a] = transport.ConductivityCount[j] > 0 ? TransportSolver.PureConductivity(in transport, j, temperature) : 0.0;
            if (transport.ViscosityCount[j] > 0)
            {
                scratch.Eta[a * Stride + a] = TransportSolver.PureViscosity(in transport, j, temperature);
            }
            else
            {
                scratch.Eta[a * Stride + a] = 0.0;
                estimatedCount++;
                estimatedFraction += scratch.Xs[a];
            }

            for (var b = 0; b < a; b++)
            {
                var pair = transport.PairIndex[j * speciesCount + scratch.IndexList[b]];
                var value = pair >= 0 ? TransportSolver.PairViscosity(in transport, pair, temperature) : 0.0;
                scratch.Eta[a * Stride + b] = value;
                scratch.Eta[b * Stride + a] = value;
            }
        }

        figures.EstimatedSpeciesCount = estimatedCount;
        figures.EstimatedMoleFraction = estimatedFraction;
    }

    /// <summary>
    /// The viscosity and conductivity of every species of the set left at zero by <see cref="Fits"/>, and the interaction
    /// viscosity of every pair left at zero.
    /// </summary>
    internal static void Estimates(in StationInputs inputs, int nm)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var temperature = inputs.Temperature;
        for (var a = 0; a < nm; a++)
        {
            var molarMass = species.MolarMass[scratch.IndexList[a]];
            if (scratch.Eta[a * Stride + a] == 0.0)
            {
                var omega = Math.Max(1.0, Math.Log(50.0 * Math.Pow(molarMass, 4.6) / Math.Pow(temperature, 1.4)));
                scratch.Eta[a * Stride + a] = 0.3125 * Math.Sqrt(TransportSolver.Boltzmann * molarMass * temperature / (Math.PI * TransportSolver.Avogadro))
                                              / (TransportSolver.CollisionDiameter * TransportSolver.CollisionDiameter * omega);
            }

            if (scratch.Cond[a] == 0.0)
            {
                scratch.Cond[a] = scratch.Eta[a * Stride + a] * (PhysicalConstants.R / molarMass) * (3.75 + 1.32 * (scratch.Cp[a] - 2.5));
            }
        }

        for (var a = 0; a < nm - 1; a++)
        {
            var massA = species.MolarMass[scratch.IndexList[a]];
            var etaA = scratch.Eta[a * Stride + a];
            for (var b = a + 1; b < nm; b++)
            {
                if (scratch.Eta[a * Stride + b] != 0.0)
                {
                    continue;
                }

                var massB = species.MolarMass[scratch.IndexList[b]];
                var ratio = Math.Sqrt(massB / massA);
                var value = 4.0 * Math.Sqrt(2.0) * etaA * Math.Sqrt(massB / (massA + massB));
                var root = 1.0 + Math.Sqrt(ratio * etaA / scratch.Eta[b * Stride + b]);
                value /= root * root;
                scratch.Eta[a * Stride + b] = value;
                scratch.Eta[b * Stride + a] = value;
            }
        }
    }
}
