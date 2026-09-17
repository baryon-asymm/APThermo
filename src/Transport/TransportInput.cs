using APThermo.Thermo;

namespace APThermo.Transport;

/// <summary>
/// Stage 1 of the evaluation: may this station be evaluated, and how much gas does it hold. The temperature, the two tables and
/// every mole number are checked, and the gaseous moles are summed in table order.
/// Scratch: reads nothing, writes nothing.
/// </summary>
internal static class TransportInput
{
    /// <summary>
    /// Returns <see cref="CaseStatus.Ok"/> with the gaseous mole sum in <paramref name="gasMoles"/>,
    /// <see cref="CaseStatus.InvalidInput"/> (non-positive or NaN temperature, an empty or gas-free table, a transport table of
    /// another species table, a negative or NaN mole number) or <see cref="CaseStatus.NoTransportData"/> (no gaseous moles).
    /// </summary>
    internal static CaseStatus Validate(in StationInputs inputs, out double gasMoles)
    {
        var species = inputs.Species;
        var speciesCount = species.SpeciesCount;
        var gasCount = species.GasCount;
        gasMoles = 0.0;
        if (!(inputs.Temperature > 0.0) || speciesCount <= 0 || gasCount <= 0 || species.ElementCount <= 0
            || inputs.Transport.SpeciesCount != speciesCount)
        {
            return CaseStatus.InvalidInput;
        }

        for (var j = 0; j < speciesCount; j++)
        {
            var nj = inputs.Moles[j];
            if (!(nj >= 0.0))
            {
                return CaseStatus.InvalidInput;
            }

            if (j < gasCount)
            {
                gasMoles += nj;
            }
        }

        return gasMoles > 0.0 ? CaseStatus.Ok : CaseStatus.NoTransportData;
    }
}
