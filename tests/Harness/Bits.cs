using System.Reflection;

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
    /// Every public <see langword="double"/> or <see langword="int"/> field or property of <typeparamref name="T"/> whose
    /// value differs between <paramref name="expected"/> and <paramref name="actual"/>, as one message per member:
    /// <c>"label.Member: expected E, actual A"</c>, doubles compared by their bits and in round-trip form, ints by
    /// <see cref="object.Equals(object, object)"/>. Throws when <typeparamref name="T"/> has no such field or property, so
    /// that a comparison can never be empty by accident (`tests/Harness/BOOT.md`, "Bits.Differences&lt;T&gt; reads fields
    /// and properties").
    /// </summary>
    public static IEnumerable<string> Differences<T>(T expected, T actual, string label) where T : struct
    {
        var members = MembersOf(typeof(T));
        return members.Count == 0
            ? throw new InvalidOperationException($"{typeof(T)} has no public double or int field or property for Bits.Differences to compare")
            : DifferencesOf(members, expected, actual, label);
    }

    private static IEnumerable<string> DifferencesOf<T>(IReadOnlyList<(string Name, Func<object, object> Read)> members, T expected, T actual, string label)
        where T : struct
    {
        foreach (var (name, read) in members)
        {
            var e = read(expected);
            var a = read(actual);
            if (e is double ed && a is double ad)
            {
                if (!Same(ed, ad))
                {
                    yield return $"{label}.{name}: expected {ed:R}, actual {ad:R}";
                }

                continue;
            }

            if (!Equals(e, a))
            {
                yield return $"{label}.{name}: expected {e}, actual {a}";
            }
        }
    }

    /// <summary>Every public instance field and property of <paramref name="type"/> whose own type is <see langword="double"/>
    /// or <see langword="int"/>, each with a boxing reader that works on both a field and a property.</summary>
    private static List<(string Name, Func<object, object> Read)> MembersOf(Type type)
    {
        var members = new List<(string Name, Func<object, object> Read)>();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(double) || field.FieldType == typeof(int))
            {
                members.Add((field.Name, obj => field.GetValue(obj)!));
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(double) || property.PropertyType == typeof(int))
            {
                members.Add((property.Name, obj => property.GetValue(obj)!));
            }
        }

        return members;
    }
}
