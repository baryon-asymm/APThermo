using System.Text.Json;

namespace APThermo.Cli.Documents;

/// <summary>A sweep entry: a non-empty list of numbers, or a range {from, to, step} expanded inclusively.</summary>
internal static class SweepValues
{
    /// <summary>
    /// Relative slack on (to − from) / step being an integer, absorbing the rounding of that division itself: far
    /// above double's rounding floor (about 1e-16) so a genuine integer step count is never rejected by rounding,
    /// and far below one step so a genuinely fractional range is never accepted.
    /// </summary>
    public const double StepTolerance = 1e-9;

    /// <summary>
    /// The most values one axis (a list or a range) may expand to (the second hidden-defect audit of 2026-09-28,
    /// finding F4, and `API.md`, Input document). A broad finite bound is checked before rounding and casting;
    /// the inclusive value bound is then checked on the rounded integer before allocation. The endpoint tolerance
    /// still admits a decimal quotient just above an otherwise valid integer (A25 follow-up, 2026-11-14).
    /// `range-3e9` still fails before its former saturated cast and misleading "not an integer" reason.
    /// </summary>
    public const int MaxAxisValues = 1_000_000;

    public static IReadOnlyList<double> Read(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            var count = value.GetArrayLength();
            if (count > MaxAxisValues)
            {
                throw new InputException($"the list at {path} has {count} values, more than the limit of {MaxAxisValues}");
            }

            var list = StrictObject.AsNumberList(value, path);
            return list.Count switch
            {
                0 => throw new InputException($"the list at {path} is empty"),
                _ => list,
            };
        }

        return value.ValueKind != JsonValueKind.Object
            ? throw new InputException($"expected a list of numbers or a range {{from, to, step}} at {path}, not {StrictObject.Describe(value)}")
            : ReadRange(new StrictObject(value, path), path);
    }

    private static double[] ReadRange(StrictObject range, string path)
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

        // Non-finite (a huge span over a tiny step) and merely too large (2^31-1 steps, or 3e9 with a false "not an
        // integer" reason once rounded to int) are refused here, before the count is rounded to an int and before
        // the "ends on a step" test below, which would otherwise run on a saturated, wrong count (F4).
        if (!(steps <= MaxAxisValues))
        {
            throw new InputException($"the range at {path} would produce more than {MaxAxisValues} values: ({to} - {from}) / {step} = {steps:R}");
        }

        var count = (int)Math.Round(steps);
        if (count >= MaxAxisValues)
        {
            throw new InputException($"the range at {path} would produce more than {MaxAxisValues} values: ({to} - {from}) / {step} = {steps:R}");
        }

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
