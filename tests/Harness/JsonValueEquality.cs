using System.Text.Json;

namespace APThermo.Harness;

/// <summary>Exact JSON instance equality for enum/const, with mathematical numeric leaves at any depth.</summary>
internal static class JsonValueEquality
{
    public static bool Equal(JsonElement left, JsonElement right) => left.ValueKind == right.ValueKind
        && left.ValueKind switch
        {
            JsonValueKind.Number => ExactJsonNumber.From(left).Compare(ExactJsonNumber.From(right)) == 0,
            JsonValueKind.Array => left.GetArrayLength() == right.GetArrayLength()
                && left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => Equal(pair.First, pair.Second)),
            JsonValueKind.Object => EqualProperties(left, right),
            JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined =>
                JsonElement.DeepEquals(left, right),
            _ => throw new ArgumentOutOfRangeException(nameof(left), left.ValueKind, "unknown JSON value kind"),
        };

    private static bool EqualProperties(JsonElement left, JsonElement right)
    {
        var remaining = new Dictionary<string, Queue<JsonElement>>(StringComparer.Ordinal);
        foreach (var property in right.EnumerateObject())
        {
            if (!remaining.TryGetValue(property.Name, out var values))
            {
                values = new Queue<JsonElement>();
                remaining.Add(property.Name, values);
            }
            values.Enqueue(property.Value);
        }
        foreach (var property in left.EnumerateObject())
        {
            if (!remaining.TryGetValue(property.Name, out var values) || values.Count == 0
                || !Equal(property.Value, values.Dequeue()))
            {
                return false;
            }
        }
        return remaining.Values.All(values => values.Count == 0);
    }
}
