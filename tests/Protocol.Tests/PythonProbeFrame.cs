using System.Text.Json;

namespace APThermo.Protocol.Tests;

/// <summary>The nonce-bound atomic state advertised by one owned probe.</summary>
internal sealed record PythonProbeFrame(string Nonce, string Mode, string Stage, int Parent, int Child)
{
    public void Validate(string nonce, string mode, string stage)
    {
        if (Nonce != nonce || Mode != mode || Stage != stage || Parent <= 0 || Child <= 0)
        {
            throw new InvalidOperationException($"fixture state mismatch: nonce={Nonce}, mode={Mode}, stage={Stage}, parent={Parent}, child={Child}");
        }
    }

    public string Serialize() => JsonSerializer.Serialize(new { nonce = Nonce, mode = Mode, stage = Stage, parent = Parent, child = Child });

    public static PythonProbeFrame Parse(string text)
    {
        using var data = JsonDocument.Parse(text);
        var root = data.RootElement;
        return new PythonProbeFrame(root.GetProperty("nonce").GetString() ?? string.Empty,
            root.GetProperty("mode").GetString() ?? string.Empty, root.GetProperty("stage").GetString() ?? string.Empty,
            root.GetProperty("parent").GetInt32(), root.GetProperty("child").GetInt32());
    }
}
