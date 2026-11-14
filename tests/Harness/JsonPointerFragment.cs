using System.Text;

namespace APThermo.Harness;

/// <summary>Strict URI-fragment decoding followed by exactly one JSON Pointer token-decoding pass.</summary>
internal static class JsonPointerFragment
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static IReadOnlyList<string> Tokens(string reference)
    {
        if (reference.Length == 0 || reference[0] != '#')
        {
            throw new InvalidOperationException($"only local URI-fragment references are supported, not '{reference}'");
        }
        var pointer = DecodeFragment(reference);
        return pointer.Length == 0 ? [] : pointer[0] == '/'
            ? [.. pointer[1..].Split('/').Select(token => DecodeToken(token, reference))]
            : throw new InvalidOperationException($"only JSON Pointer fragments are supported, not '{reference}'");
    }

    private static string DecodeFragment(string reference)
    {
        var result = new StringBuilder();
        var index = 1;
        while (index < reference.Length)
        {
            if (reference[index] == '%')
            {
                _ = result.Append(DecodePercentRun(reference, ref index));
                continue;
            }
            var character = reference[index++];
            if (!char.IsAsciiLetterOrDigit(character) && !"-._~!$&'()*+,;=:@/?".Contains(character, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"invalid URI-fragment character in schema reference '{reference}'");
            }
            _ = result.Append(character);
        }
        return result.ToString();
    }

    private static string DecodePercentRun(string reference, ref int index)
    {
        var bytes = new List<byte>();
        while (index < reference.Length && reference[index] == '%')
        {
            if (!Uri.IsHexEncoding(reference, index))
            {
                throw new InvalidOperationException($"invalid percent encoding in schema reference '{reference}'");
            }
            bytes.Add((byte)(16 * Uri.FromHex(reference[index + 1]) + Uri.FromHex(reference[index + 2])));
            index += 3;
        }
        byte[] encoded = [.. bytes];
        try
        {
            return StrictUtf8.GetString(encoded);
        }
        catch (DecoderFallbackException problem)
        {
            throw new InvalidOperationException($"invalid UTF-8 in schema reference '{reference}'", problem);
        }
    }

    private static string DecodeToken(string token, string reference)
    {
        var result = new StringBuilder();
        for (var index = 0; index < token.Length; index++)
        {
            if (token[index] != '~')
            {
                _ = result.Append(token[index]);
                continue;
            }
            if (++index == token.Length || token[index] is not ('0' or '1'))
            {
                throw new InvalidOperationException($"invalid JSON Pointer escape in schema reference '{reference}'");
            }
            _ = result.Append(token[index] == '0' ? '~' : '/');
        }
        return result.ToString();
    }
}
