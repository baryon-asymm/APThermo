namespace AerospacePropellantThermodynamics.Harness;

/// <summary>
/// Bit-for-bit equality of doubles and structs: the standard the tree's kernel-equality and accelerator-equality facts
/// are measured against (BOOT.md, "bits are raw bits"). Two doubles are the same when
/// <see cref="BitConverter.DoubleToInt64Bits(double)"/> agrees, so that signed zeros and NaN payloads are told apart;
/// nothing here rounds or tolerates.
/// </summary>
public static class Bits
{
    public static bool Same(double expected, double actual) =>
        BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual);

    /// <summary>
    /// Every public double or int field of <typeparamref name="T"/> whose bits differ between <paramref name="expected"/>
    /// and <paramref name="actual"/>, as one message per field: <c>"label.Field: expected E, actual A"</c>, doubles in
    /// round-trip form. A field of another type is compared with <see cref="object.Equals(object, object)"/>.
    /// </summary>
    public static IEnumerable<string> Differences<T>(T expected, T actual, string label) where T : struct
    {
        foreach (var field in typeof(T).GetFields())
        {
            var e = field.GetValue(expected)!;
            var a = field.GetValue(actual)!;
            if (e is double ed && a is double ad)
            {
                if (!Same(ed, ad))
                {
                    yield return $"{label}.{field.Name}: expected {ed:R}, actual {ad:R}";
                }

                continue;
            }

            if (!Equals(e, a))
            {
                yield return $"{label}.{field.Name}: expected {e}, actual {a}";
            }
        }
    }
}
