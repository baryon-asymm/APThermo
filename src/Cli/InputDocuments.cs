using System.Text.Json;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The readers of the input documents (API.md), the façade the tests node uses: delegations only.</summary>
internal static class InputDocuments
{
    public static InputDocument ReadProblem(string text, string source) => ProblemDocumentReader.Read(text, source);

    /// <summary>The records of one or more files: a JSON array, a single object, or JSON Lines.</summary>
    public static IReadOnlyList<StateDocument> ReadStates(IReadOnlyList<(string Source, string Text)> files) => StateRecordReader.Read(files);

    public static IReadOnlyList<double> ReadValues(JsonElement value, string path) => SweepValues.Read(value, path);
}
