using System.Text;
using System.Text.Json;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>
/// The bytes of a JSON object with its top-level `run` property cut out (BOOT.md, the Bits level): the property's
/// name, its value and one adjacent separator with the white space around it, so the remaining bytes read as the same
/// document written without that property —
/// after a preceding property, from the separator following that property's value to the end of `run`'s value;
/// as the first property, from `run`'s name to the start of the next property's name.
/// The span is found by walking the top-level properties in order with a <see cref="Utf8JsonReader"/>: the token
/// start of each property name, then <see cref="Utf8JsonReader.Skip"/> to the end of its value — never by searching
/// the text for the literal `"run"`, so a value that happens to contain that text elsewhere in the document cannot be
/// mistaken for the property. Indentation, line breaks, the final newline, string escaping and the key order of every
/// other property all stay in. A document with no top-level `run` property, or with more than one, throws instead of
/// producing a result, naming <paramref name="example"/>: the caller's test fails and no hash is computed from it.
/// </summary>
internal static class RunPropertyCut
{
    public static byte[] Bytes(byte[] document, string example)
    {
        var properties = TopLevelProperties(document, example);
        var runIndex = properties.FindIndex(p => p.IsRun);
        if (runIndex < 0)
        {
            throw new InvalidOperationException($"{example}: no top-level 'run' property");
        }

        return Cut(document, properties, runIndex, example);
    }

    private static List<Property> TopLevelProperties(byte[] document, string example)
    {
        var reader = new Utf8JsonReader(document);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException($"{example}: not a JSON object at the top level");
        }

        var properties = new List<Property>();
        var runSeen = false;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new InvalidOperationException($"{example}: expected a top-level property name");
            }

            var nameStart = reader.TokenStartIndex;
            var isRun = reader.ValueTextEquals("run"u8);
            if (isRun && runSeen)
            {
                throw new InvalidOperationException($"{example}: more than one top-level 'run' property");
            }

            runSeen |= isRun;
            reader.Skip();
            properties.Add(new Property(nameStart, reader.BytesConsumed, isRun));
        }

        return properties;
    }

    private static byte[] Cut(byte[] document, List<Property> properties, int runIndex, string example)
    {
        long cutStart;
        long cutEnd;
        if (runIndex == 0)
        {
            if (properties.Count == 1)
            {
                throw new InvalidOperationException($"{example}: the document has only a top-level 'run' property");
            }

            cutStart = properties[0].NameStart;
            cutEnd = properties[1].NameStart;
        }
        else
        {
            cutStart = properties[runIndex - 1].ValueEnd;
            cutEnd = properties[runIndex].ValueEnd;
        }

        var result = new byte[document.Length - (cutEnd - cutStart)];
        document.AsSpan(0, (int)cutStart).CopyTo(result);
        document.AsSpan((int)cutEnd).CopyTo(result.AsSpan((int)cutStart));
        return result;
    }

    private readonly record struct Property(long NameStart, long ValueEnd, bool IsRun);
}

/// <summary>
/// The cut is exact wherever the top-level `run` property sits among its siblings: first, in the middle or last, the
/// result reads as the same document written without it (<see cref="RunPropertyCut"/>, above). Seen red once with the
/// span shifted by one byte (<c>document.AsSpan((int)cutEnd + 1)</c> in <c>RunPropertyCut.Cut</c>): all three cases
/// failed (`Assert.Equal() Failure: Strings differ`), each missing exactly the one byte immediately after the removed
/// span — the next property's opening quote when `run` is first, the separator comma when it is not — and the "run
/// last" case besides carried a trailing NUL from the now-oversized destination span; reverted immediately.
/// </summary>
public sealed class RunPropertyCutTests
{
    private const string RunFirst = "{\n  \"run\": {\n    \"a\": 1\n  },\n  \"before\": false,\n  \"after\": [\n    1,\n    2\n  ]\n}";
    private const string RunMiddle = "{\n  \"before\": false,\n  \"run\": {\n    \"a\": 1\n  },\n  \"after\": [\n    1,\n    2\n  ]\n}";
    private const string RunLast = "{\n  \"before\": false,\n  \"after\": [\n    1,\n    2\n  ],\n  \"run\": {\n    \"a\": 1\n  }\n}";
    private const string WithoutRun = "{\n  \"before\": false,\n  \"after\": [\n    1,\n    2\n  ]\n}";

    [Theory]
    [InlineData(RunFirst)]
    [InlineData(RunMiddle)]
    [InlineData(RunLast)]
    public void The_top_level_run_property_is_cut_wherever_it_appears(string withRun)
    {
        var cut = RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(withRun), "test document");
        Assert.Equal(WithoutRun, Encoding.UTF8.GetString(cut));
    }

    [Fact]
    public void A_missing_top_level_run_property_fails_instead_of_hashing()
    {
        var withoutRun = Encoding.UTF8.GetBytes(WithoutRun);
        var exception = Assert.Throws<InvalidOperationException>(() => RunPropertyCut.Bytes(withoutRun, "no run"));
        Assert.Contains("no top-level 'run' property", exception.Message);
    }

    [Fact]
    public void A_duplicated_top_level_run_property_fails_instead_of_hashing()
    {
        const string doubled = "{\n  \"run\": 1,\n  \"before\": false,\n  \"run\": 2\n}";
        var exception = Assert.Throws<InvalidOperationException>(() => RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(doubled), "doubled run"));
        Assert.Contains("more than one top-level 'run' property", exception.Message);
    }
}
