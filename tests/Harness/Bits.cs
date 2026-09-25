namespace APThermo.Harness;

/// <summary>
/// Bit-for-bit equality of doubles and structs: the standard the tree's kernel-equality and accelerator-equality facts
/// are measured against (BOOT.md, "bits are raw bits"). Two doubles are the same when
/// <see cref="BitConverter.DoubleToInt64Bits(double)"/> agrees, so that signed zeros and NaN payloads are told apart;
/// nothing here rounds or tolerates.
/// </summary>
public static class Bits
{
    /// <summary>Whether two doubles are bit-for-bit the same, signed zeros and NaN payloads told apart.</summary>
    /// <param name="expected">The reference value.</param>
    /// <param name="actual">The tree's value.</param>
    /// <returns><see langword="true"/> when the two values' bits are identical.</returns>
    public static bool Same(double expected, double actual) =>
        BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual);

    /// <summary>
    /// Every public double or int property of <typeparamref name="T"/> whose bits differ between
    /// <paramref name="expected"/> and <paramref name="actual"/>, as one message per property:
    /// <c>"label.Property: expected E, actual A"</c>, doubles in round-trip form. A property of another type is
    /// compared with <see cref="object.Equals(object, object)"/>.
    /// </summary>
    public static IEnumerable<string> Differences<T>(T expected, T actual, string label) where T : struct
    {
        foreach (var property in typeof(T).GetProperties())
        {
            var e = property.GetValue(expected)!;
            var a = property.GetValue(actual)!;
            if (e is double ed && a is double ad)
            {
                if (!Same(ed, ad))
                {
                    yield return $"{label}.{property.Name}: expected {ed:R}, actual {ad:R}";
                }

                continue;
            }

            if (!Equals(e, a))
            {
                yield return $"{label}.{property.Name}: expected {e}, actual {a}";
            }
        }
    }
}
