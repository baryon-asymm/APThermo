using System.Text.Json;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>A sweep entry: a non-empty list of numbers, or a range {from, to, step} expanded inclusively.</summary>
internal static class SweepValues
{
    /// <summary>
    /// Relative slack on (to − from) / step being an integer, absorbing the rounding of that division itself: far
    /// above double's rounding floor (about 1e-16) so a genuine integer step count is never rejected by rounding,
    /// and far below one step so a genuinely fractional range is never accepted.
    /// </summary>
    public const double StepTolerance = 1e-9;

    public static IReadOnlyList<double> Read(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            var list = StrictObject.AsNumberList(value, path);
            if (list.Count == 0)
            {
                throw new InputException($"the list at {path} is empty");
            }

            return list;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InputException($"expected a list of numbers or a range {{from, to, step}} at {path}, not {StrictObject.Describe(value)}");
        }

        return ReadRange(new StrictObject(value, path), path);
    }

    private static IReadOnlyList<double> ReadRange(StrictObject range, string path)
    {
        var from = range.Number("from");
        var to = range.Number("to");
        var step = range.Number("step");
        range.Finish();
        if (!(step > 0.0))
        {
            throw new InputException($"the step at {path} must be positive");
        }

        if (to < from)
        {
            throw new InputException($"the range at {path} ends before it starts");
        }

        var steps = (to - from) / step;
        var count = (int)Math.Round(steps);
        if (Math.Abs(steps - count) > StepTolerance * Math.Max(1.0, Math.Abs(steps)))
        {
            throw new InputException($"the range at {path} does not end on a step: ({to} - {from}) / {step} is not an integer");
        }

        var values = new double[count + 1];
        for (var k = 0; k <= count; k++)
        {
            values[k] = k == count ? to : from + k * step;
        }

        return values;
    }
}
