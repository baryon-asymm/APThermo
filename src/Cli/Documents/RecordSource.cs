using System.Text.Json;

namespace APThermo.Cli.Documents;

/// <summary>Where a state record came from: its position in the whole list given, the label naming it in a message, and its raw JSON for the echo.</summary>
internal sealed record RecordSource(int Index, string Label, JsonElement Raw);
