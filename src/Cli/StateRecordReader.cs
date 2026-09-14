using System.Text.Json;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// Reads the state-record files of the states command: a JSON array, a single object, or JSON Lines (API.md, Command
/// line). The shape is decided without an exception as a probe (<see cref="JsonText.TryParseWhole"/>, F-CL-13). Only
/// the record's JSON shape is read here, into the front door's own <see cref="StateRecord"/>; the rules of a record
/// (exactly one target, exits needing an enthalpy, a flow only with exits) are the front door's, left to
/// <see cref="Solver.SolveStates"/> and <see cref="Solver.SolveRocketStates"/> (F-AR-02).
/// </summary>
internal static class StateRecordReader
{
    public static IReadOnlyList<(StateRecord Record, RecordSource Source)> Read(IReadOnlyList<(string Source, string Text)> files)
    {
        var records = new List<(StateRecord, RecordSource)>();
        foreach (var (source, text) in files)
        {
            foreach (var (element, label) in RecordElements(text, source))
            {
                records.Add((ReadRecord(element, label), new RecordSource(records.Count, label, element)));
            }
        }

        if (records.Count == 0)
        {
            throw new InputException("no state record was given");
        }

        return records;
    }

    private static IReadOnlyList<(JsonElement Element, string Label)> RecordElements(string text, string source)
    {
        if (JsonText.TryParseWhole(text, out var whole))
        {
            using (whole)
            {
                var root = whole.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    return root.EnumerateArray().Select((item, i) => (item.Clone(), $"{source}: record {i}")).ToList();
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    return [(root.Clone(), $"{source}: record 0")];
                }

                throw new InputException($"{source}: expected an array of records, one record or JSON Lines, not {StrictObject.Describe(root)}");
            }
        }

        return LinesOf(text, source);
    }

    private static IReadOnlyList<(JsonElement Element, string Label)> LinesOf(string text, string source)
    {
        var records = new List<(JsonElement, string)>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            using var document = JsonText.ParseLine(line, source, i + 1);
            records.Add((document.RootElement.Clone(), $"{source}:{i + 1}"));
        }

        return records;
    }

    private static StateRecord ReadRecord(JsonElement element, string label)
    {
        try
        {
            var record = new StrictObject(element, "$");
            var pressure = record.Number("pressure");
            var composition = record.NumberMap("composition");
            var enthalpy = record.OptionalNumber("enthalpy");
            var temperature = record.OptionalNumber("temperature");
            var entropy = record.OptionalNumber("entropy");
            var areaRatios = record.OptionalNumberList("areaRatios") ?? [];
            var pressureRatios = record.OptionalNumberList("pressureRatios") ?? [];
            var flowName = record.OptionalString("flow");
            record.Finish();
            return new StateRecord(pressure, composition, enthalpy, temperature, entropy)
            {
                AreaRatios = areaRatios,
                PressureRatios = pressureRatios,
                Flow = flowName is null ? null : DocumentWords.ParseFlow(flowName, "$.flow"),
            };
        }
        catch (InputException e)
        {
            throw new InputException($"{label}: {e.Message}");
        }
    }
}
