using System.Text.Json;

namespace APThermo.Harness;

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
public static class RunPropertyCut
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
