using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace APThermo.Harness;

/// <summary>Normalized decimal digits and an unbounded exponent; operations never expand exponent-sized powers.</summary>
internal readonly record struct ExactJsonNumber
{
    private readonly int _sign;
    private readonly string _digits;
    private readonly BigInteger _exponent;

    private ExactJsonNumber(int sign, string digits, BigInteger exponent)
    {
        _sign = sign;
        _digits = digits;
        _exponent = exponent;
    }

    public bool IsInteger => _sign == 0 || _exponent.Sign >= 0;

    public static ExactJsonNumber From(JsonElement number) => number.ValueKind == JsonValueKind.Number
        ? Parse(number.GetRawText()) : throw new InvalidOperationException("expected a JSON number");

    public static ExactJsonNumber FromInt32(int number) => Parse(number.ToString(CultureInfo.InvariantCulture));

    public int Compare(ExactJsonNumber other)
    {
        var signOrder = _sign.CompareTo(other._sign);
        if (signOrder != 0 || _sign == 0)
        {
            return signOrder;
        }
        var magnitudeOrder = (_exponent + _digits.Length).CompareTo(other._exponent + other._digits.Length);
        if (magnitudeOrder != 0)
        {
            return _sign * magnitudeOrder;
        }
        var count = Math.Max(_digits.Length, other._digits.Length);
        for (var index = 0; index < count; index++)
        {
            var left = index < _digits.Length ? _digits[index] : '0';
            var right = index < other._digits.Length ? other._digits[index] : '0';
            if (left != right)
            {
                return _sign * left.CompareTo(right);
            }
        }
        return 0;
    }

    private static ExactJsonNumber Parse(string text)
    {
        // Callers supply an already-validated JSON number token or an invariant Int32.
        var exponentAt = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentAt < 0 ? text : text[..exponentAt];
        var sign = mantissa[0] == '-' ? -1 : 1;
        var coefficient = mantissa.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('-', '0');
        if (coefficient.Length == 0)
        {
            return new ExactJsonNumber(0, "0", BigInteger.Zero);
        }
        var digits = coefficient.TrimEnd('0');
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var fractionLength = dot < 0 ? 0 : mantissa.Length - dot - 1;
        var exponent = exponentAt < 0 ? BigInteger.Zero : ParseExponent(text.AsSpan(exponentAt + 1));
        return new ExactJsonNumber(sign, digits, exponent - fractionLength + coefficient.Length - digits.Length);
    }

    private static BigInteger ParseExponent(ReadOnlySpan<char> text)
    {
        var negative = text[0] == '-';
        var digits = text[0] is '-' or '+' ? text[1..] : text;
        digits = digits.TrimStart('0');
        var magnitude = digits.IsEmpty ? BigInteger.Zero : BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        return negative ? -magnitude : magnitude;
    }
}
