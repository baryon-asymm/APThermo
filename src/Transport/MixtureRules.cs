namespace AerospacePropellantThermodynamics.Transport;

/// <summary>
/// Stage 11 of the evaluation: the mixture viscosity and the frozen conductivity of the transport set, RP-1311 equations (5.3)
/// and (5.4), with the interaction terms φ_ij of (5.7) and ψ_ij of (5.6).
/// Scratch: reads <c>IndexList</c>, <c>Xs</c>, <c>Eta</c>, <c>Cond</c>; writes nothing.
/// </summary>
internal static class MixtureRules
{
    private const int Stride = TransportSolver.Stride;

    /// <summary>The viscosity and the frozen conductivity of the set of <paramref name="nm"/> species, SI.</summary>
    internal static MixtureTransport Evaluate(in StationInputs inputs, int nm)
    {
        var species = inputs.Species;
        var scratch = inputs.Scratch;
        var viscosity = 0.0;
        var frozenConductivity = 0.0;
        for (var a = 0; a < nm; a++)
        {
            var massA = species.MolarMass[scratch.IndexList[a]];
            var etaA = scratch.Eta[a * Stride + a];
            var sumViscosity = scratch.Xs[a];
            var sumConductivity = scratch.Xs[a];
            for (var b = 0; b < nm; b++)
            {
                if (b == a)
                {
                    continue;
                }

                var massB = species.MolarMass[scratch.IndexList[b]];
                var phi = 2.0 * massB * etaA / (scratch.Eta[a * Stride + b] * (massA + massB));
                var psi = phi * (1.0 + 2.41 * (massA - massB) * (massA - 0.142 * massB) / ((massA + massB) * (massA + massB)));
                sumViscosity += phi * scratch.Xs[b];
                sumConductivity += psi * scratch.Xs[b];
            }

            viscosity += etaA * scratch.Xs[a] / sumViscosity;
            frozenConductivity += scratch.Cond[a] * scratch.Xs[a] / sumConductivity;
        }

        return new MixtureTransport(viscosity, frozenConductivity);
    }
}
