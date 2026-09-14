namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>Bit-for-bit equality of doubles and structs, the standard the node's L2 facts are measured against.</summary>
internal static class BitEquality
{
    public static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    /// <summary>Field-by-field bit equality of two structs.</summary>
    public static IEnumerable<string> BitDifferences<T>(T expected, T actual, string label) where T : struct
    {
        foreach (var field in typeof(T).GetFields())
        {
            var a = field.GetValue(expected)!;
            var b = field.GetValue(actual)!;
            var same = a is double x && b is double y ? SameBits(x, y) : a.Equals(b);
            if (!same)
            {
                yield return $"{label} {field.Name}: {a} vs {b}";
            }
        }
    }
}
