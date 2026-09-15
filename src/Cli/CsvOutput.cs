using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// The CSV form: one row per case and station, the scalar inputs, the state, the figures and the transport figures; no
/// compositions. The cells after <c>station</c> and <c>status</c> are those of <see cref="StationFields"/>, with
/// <c>transportStatus</c> before the transport cells.
/// </summary>
internal static class CsvOutput
{
    public static string Render(IReadOnlyList<CaseOutput> cases)
    {
        var inputColumns = InputColumnsOf(cases);
        var text = new StringBuilder();
        text.Append(string.Join(",", HeaderOf(inputColumns).Select(Escape))).Append('\n');
        foreach (var c in cases)
        {
            var inputs = ScalarInputs(c.Inputs).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
            foreach (var station in c.Stations)
            {
                text.Append(string.Join(",", RowOf(c, station, inputColumns, inputs).Select(Escape))).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Every scalar of the cases' <c>inputs</c> (numbers, strings, booleans; a record's objects and lists are not
    /// columns), in the order first seen; a case without one leaves its cell empty.
    /// </summary>
    private static IReadOnlyList<string> InputColumnsOf(IReadOnlyList<CaseOutput> cases)
    {
        var columns = new List<string>();
        foreach (var c in cases)
        {
            foreach (var (name, _) in ScalarInputs(c.Inputs))
            {
                if (!columns.Contains(name, StringComparer.Ordinal))
                {
                    columns.Add(name);
                }
            }
        }

        return columns;
    }

    private static IReadOnlyList<string> HeaderOf(IReadOnlyList<string> inputColumns)
    {
        var header = new List<string> { "case" };
        header.AddRange(inputColumns);
        header.AddRange(["station", "status"]);
        header.AddRange(StationFields.StateNames);
        header.AddRange(StationFields.PerformanceNames);
        header.Add("transportStatus");
        header.AddRange(StationFields.TransportNames);
        return header;
    }

    private static IReadOnlyList<string> RowOf(CaseOutput c, Station station, IReadOnlyList<string> inputColumns, IReadOnlyDictionary<string, string> inputs)
    {
        var cells = new List<string> { c.Index.ToString(CultureInfo.InvariantCulture) };
        cells.AddRange(inputColumns.Select(name => inputs.TryGetValue(name, out var value) ? value : ""));
        cells.Add(station.Name);
        cells.Add(Names.Status(station.Status));
        cells.AddRange(StationFields.State(station.State).Select(cell => Number(cell.Number!.Value)));
        if (station.Performance is { } figures)
        {
            cells.AddRange(StationFields.Performance(figures).Select(cell => Number(cell.Number!.Value)));
        }
        else
        {
            cells.AddRange(Enumerable.Repeat("", StationFields.PerformanceNames.Count));
        }

        cells.Add(station.TransportStatus is { } transportStatus ? Names.Status(transportStatus) : "");
        if (station.Transport is { } transport)
        {
            cells.AddRange(StationFields.Transport(transport).Select(cell => cell.Integer is { } i ? i.ToString(CultureInfo.InvariantCulture) : Number(cell.Number!.Value)));
        }
        else
        {
            cells.AddRange(Enumerable.Repeat("", StationFields.TransportNames.Count));
        }

        return cells;
    }

    private static IEnumerable<(string Name, string Value)> ScalarInputs(JsonNode inputs)
    {
        if (inputs is not JsonObject obj)
        {
            yield break;
        }

        foreach (var (name, node) in obj)
        {
            if (node is not JsonValue value)
            {
                continue;
            }

            if (value.TryGetValue<double>(out var number))
            {
                yield return (name, Number(number));
            }
            else if (value.TryGetValue<string>(out var text))
            {
                yield return (name, text);
            }
            else if (value.TryGetValue<bool>(out var flag))
            {
                yield return (name, flag ? "true" : "false");
            }
        }
    }

    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static string Escape(string cell) =>
        cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
}
