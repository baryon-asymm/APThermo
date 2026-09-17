using System.Globalization;

namespace APThermo.Data;

/// <summary>
/// Reads numbers the way Fortran formatted output writes them: <c>D</c> or <c>E</c> exponent letters,
/// an exponent sign replaced by a blank (<c>0.61E 02</c>), an exponent without a letter (<c>1.5-03</c>),
/// and a blank field meaning zero.
/// </summary>
internal static class FortranNumber
{
    public static double Parse(string field)
    {
        var text = field.Trim();
        if (text.Length == 0)
        {
            return 0.0;
        }

        var normalized = Normalize(text);
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"'{field}' is not a number");
        }

        return value;
    }

    public static int ParseInt(string field)
    {
        var text = field.Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"'{field}' is not an integer");
        }

        return value;
    }

    private static string Normalize(string text)
    {
        var chars = text.ToCharArray();
        var exponentAt = -1;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is 'D' or 'd' or 'E' or 'e')
            {
                chars[i] = 'E';
                exponentAt = i;
                break;
            }
        }

        var s = new string(chars);
        if (exponentAt >= 0)
        {
            // "E 02" → "E+02"; "E  02" likewise.
            var mantissa = s[..(exponentAt + 1)];
            var exponent = s[(exponentAt + 1)..].TrimStart();
            if (exponent.Length > 0 && char.IsDigit(exponent[0]))
            {
                exponent = "+" + exponent;
            }

            return mantissa + exponent.Replace(" ", string.Empty);
        }

        // "1.5-03" → "1.5E-03": a sign inside the number after the first character.
        for (var i = 1; i < s.Length; i++)
        {
            if (s[i] is '+' or '-' && char.IsDigit(s[i - 1]) || s[i] is '+' or '-' && s[i - 1] == '.')
            {
                return s[..i] + "E" + s[i..];
            }
        }

        return s;
    }
}
