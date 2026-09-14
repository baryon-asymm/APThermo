using System.Text.Json;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// Reads the state-record files of the states command: a JSON array, a single object, or JSON Lines (API.md, Command
/// line). The shape is decided without an exception as a probe (<see cref="JsonText.TryParseWhole"/>, F-CL-13).
/// </summary>
internal static class StateRecordReader
{
    public static IReadOnlyList<StateDocument> Read(IReadOnlyList<(string Source, string Text)> files)
    {
        var records = new List<StateDocument>();
        foreach (var (source, text) in files)
        {
            foreach (var (element, label) in RecordElements(text, source))
            {
                records.Add(ReadState(element, label, records.Count));
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

    private static StateDocument ReadState(JsonElement element, string label, int index)
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
            CheckExactlyOneTarget(enthalpy, temperature, entropy);
            var isRocket = areaRatios.Count + pressureRatios.Count > 0;
            if (isRocket && enthalpy is null)
            {
                throw new InputException("a record with exits is a rocket case and needs 'enthalpy'");
            }

            if (flowName is not null && !isRocket)
            {
                throw new InputException("'flow' belongs to a record with exits");
            }

            var flow = DocumentWords.ParseFlow(flowName ?? DocumentWords.FlowShifting, "$.flow");
            return new StateDocument(index, label, element, pressure, composition)
            {
                Enthalpy = enthalpy,
                Temperature = temperature,
                Entropy = entropy,
                AreaRatios = areaRatios,
                PressureRatios = pressureRatios,
                Flow = flow,
            };
        }
        catch (InputException e)
        {
            throw new InputException($"{label}: {e.Message}");
        }
    }

    private static void CheckExactlyOneTarget(double? enthalpy, double? temperature, double? entropy)
    {
        var targets = (enthalpy is null ? 0 : 1) + (temperature is null ? 0 : 1) + (entropy is null ? 0 : 1);
        if (targets != 1)
        {
            throw new InputException($"exactly one of enthalpy, temperature and entropy must be given, not {targets}");
        }
    }
}
