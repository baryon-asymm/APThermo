using System.Collections.ObjectModel;

namespace APThermo.Problems;

/// <summary>Owned publication snapshots of the front door's collection-valued data.</summary>
internal static class CollectionSnapshot
{
    internal static IReadOnlyList<T> List<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    internal static IReadOnlyList<T>? Optional<T>(IReadOnlyList<T>? values) =>
        values is null ? null : List(values);

    internal static IReadOnlyDictionary<string, double> Dictionary(IReadOnlyDictionary<string, double> values) =>
        new ReadOnlyDictionary<string, double>(values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
}
