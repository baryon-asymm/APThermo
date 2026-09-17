using System.Text;
using System.Text.Json;

namespace APThermo.Cli.Documents;

/// <summary>A text parsed as JSON with its source label folded into the message: a file path, or a file and a line.</summary>
internal static class JsonText
{
    public static JsonDocument Parse(string text, string source)
    {
        try
        {
            return JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        }
        catch (JsonException e)
        {
            throw new InputException($"{source}: malformed JSON: {e.Message}");
        }
    }

    public static JsonDocument ParseLine(string line, string source, int lineNumber)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException e)
        {
            throw new InputException($"{source}:{lineNumber}: malformed JSON: {e.Message}");
        }
    }

    /// <summary>
    /// Whether <paramref name="text"/> is one JSON value (an object or an array), decided by attempting the parse and
    /// checking that nothing but whitespace follows it: a text of several JSON values with no separator (JSON Lines)
    /// fails this check by running out of data, without throwing, so that the caller falls back to reading it line by
    /// line; a text whose first value is itself malformed throws (<c>JsonDocument.TryParseValue</c> returns false only
    /// when data runs out), caught here and reported as an <see cref="InputException"/> naming <paramref name="source"/>.
    /// </summary>
    public static bool TryParseWhole(string text, string source, out JsonDocument document)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
        try
        {
            if (!JsonDocument.TryParseValue(ref reader, out document!))
            {
                document = null!;
                return false;
            }
        }
        catch (JsonException e)
        {
            throw new InputException($"{source}: malformed JSON: {e.Message}");
        }

        for (var i = (int)reader.BytesConsumed; i < bytes.Length; i++)
        {
            if (!IsJsonWhitespace(bytes[i]))
            {
                document.Dispose();
                document = null!;
                return false;
            }
        }

        return true;
    }

    private static bool IsJsonWhitespace(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
