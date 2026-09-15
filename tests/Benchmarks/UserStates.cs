using System.Text.Json;
using APThermo.Problems;

namespace APThermo.Benchmarks;

/// Reads `data/user-states.json` (BOOT.md, Invariants: the user's state records are a
/// data file of this node, with the pressures given as a rule — first, step, count —
/// and the code computes `first + i * step` by index, never by accumulation).
internal static class UserStates
{
    public static IReadOnlyList<IReadOnlyList<StateRecord>> ReadByRecord(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var rule = root.GetProperty("pressureRule");
        var first = rule.GetProperty("firstPascal").GetDouble();
        var step = rule.GetProperty("stepPascal").GetDouble();
        var count = rule.GetProperty("count").GetInt32();

        var byRecord = new List<IReadOnlyList<StateRecord>>();
        foreach (var record in root.GetProperty("records").EnumerateArray())
        {
            byRecord.Add(StatesOf(record, first, step, count));
        }
        return byRecord;
    }

    private static List<StateRecord> StatesOf(JsonElement record, double first, double step, int count)
    {
        var enthalpy = record.GetProperty("enthalpyPerKilogram").GetDouble();
        var composition = new Dictionary<string, double>();
        foreach (var element in record.GetProperty("elementMolesPerKilogram").EnumerateObject())
        {
            composition[element.Name] = element.Value.GetDouble();
        }

        var states = new List<StateRecord>(count);
        for (var i = 0; i < count; i++)
        {
            states.Add(new StateRecord(first + i * step, composition, enthalpy));
        }
        return states;
    }
}
