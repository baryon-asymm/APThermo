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
    /// finding F4, and `API.md`, Input document). A range's own step count is checked against this limit before it
    /// is rounded to an <c>int</c>, so the "ends on a step" test below never runs on a step count the limit would
    /// already refuse, and its reason is never false (`range-3e9`: 3e9 steps used to saturate to <see cref="int.MaxValue"/>
    /// first and fail that test with a misleading reason).
    /// </summary>
    public const int MaxAxisValues = 1_000_000;

    public static IReadOnlyList<double> Read(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            var list = StrictObject.AsNumberList(value, path);
            return list.Count switch
            {
                0 => throw new InputException($"the list at {path} is empty"),
                > MaxAxisValues => throw new InputException($"the list at {path} has {list.Count} values, more than the limit of {MaxAxisValues}"),
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
