using System.Text.Json.Nodes;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The `inputs` echo of a sweep case, written once: the ratio, when the sweep or the propellant names one.</summary>
internal static class CaseInputs
{
    public static JsonObject Start(double? swept, double? ownRatio)
    {
        var inputs = new JsonObject();
        if ((swept ?? ownRatio) is { } ratio)
        {
            inputs["oxidizerToFuel"] = ratio;
        }

        return inputs;
    }
}
