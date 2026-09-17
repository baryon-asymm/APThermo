using System.Reflection;
using APThermo.Harness;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Problems.Tests;

/// <summary>
/// Bit and relative equality of two stations of the tree's own code (this node's BOOT.md, invariants): two solves that must
/// agree exactly, or to a stated relative tolerance where a union batch reordered a case's elements.
/// </summary>
internal static class StationEquality
{
    /// <summary>Field-by-field bit equality of two stations: state, figures, mole fractions, condensed mass fractions and transport figures.</summary>
    public static IEnumerable<string> BitDifferences(Station expected, Station actual, string label)
    {
        foreach (var difference in Bits.Differences(expected.State, actual.State, label + " state"))
        {
            yield return difference;
        }

        if (expected.Performance.HasValue != actual.Performance.HasValue)
        {
            yield return $"{label}: performance figures present on one side only";
        }
        else if (expected.Performance is { } figures)
        {
            foreach (var difference in Bits.Differences(figures, actual.Performance!.Value, label + " figures"))
            {
                yield return difference;
            }
        }

        if (expected.Transport.HasValue != actual.Transport.HasValue)
        {
            yield return $"{label}: transport figures present on one side only";
        }
        else if (expected.Transport is { } transport)
        {
            foreach (var difference in Bits.Differences(transport, actual.Transport!.Value, label + " transport"))
            {
                yield return difference;
            }
        }

        if (expected.Status != actual.Status || expected.TransportStatus != actual.TransportStatus)
        {
            yield return $"{label}: statuses differ";
        }

        foreach (var (name, x) in expected.MoleFractions)
        {
            if (!actual.MoleFractions.TryGetValue(name, out var y) || !Bits.Same(x, y))
            {
                yield return $"{label} x({name}): {x:R} vs {(actual.MoleFractions.TryGetValue(name, out var v) ? v.ToString("R") : "missing")}";
            }
        }

        foreach (var (name, x) in expected.CondensedMassFractions)
        {
            if (!actual.CondensedMassFractions.TryGetValue(name, out var y) || !Bits.Same(x, y))
            {
                yield return $"{label} w({name}) differs";
            }
        }
    }

    /// <summary>
    /// Two stations within a relative tolerance on every double field (state, figures, transport), mole fractions above the floor
    /// included, and exactly on statuses and counts; for a batch whose element order changed the pivot order of the linear solves.
    /// </summary>
    public static IEnumerable<string> RelativeDifferences(Station expected, Station actual, double relative, double moleFractionFloor, string label)
    {
        static bool Close(double p, double q, double relative) => Math.Abs(p - q) <= relative * Math.Max(Math.Abs(p), Math.Abs(q));

        if (expected.Status != actual.Status || expected.TransportStatus != actual.TransportStatus)
        {
            yield return $"{label}: statuses differ";
        }

        foreach (var field in typeof(MixtureState).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var p = (double)field.GetValue(expected.State)!;
            var q = (double)field.GetValue(actual.State)!;
            if (!Close(p, q, relative))
            {
                yield return $"{label} state {field.Name}: {p:R} vs {q:R}";
            }
        }

        if (expected.Performance.HasValue != actual.Performance.HasValue)
        {
            yield return $"{label}: performance figures present on one side only";
        }
        else if (expected.Performance is { } figures)
        {
            foreach (var field in typeof(PerformanceFigures).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var p = (double)field.GetValue(figures)!;
                var q = (double)field.GetValue(actual.Performance!.Value)!;
                if (!Close(p, q, relative))
                {
                    yield return $"{label} figures {field.Name}: {p:R} vs {q:R}";
                }
            }
        }

        foreach (var difference in TransportDifferences(expected, actual, relative, label))
        {
            yield return difference;
        }

        foreach (var (name, p) in expected.MoleFractions)
        {
            var q = actual.MoleFractions.GetValueOrDefault(name);
            if (Math.Max(p, q) >= moleFractionFloor && !Close(p, q, relative))
            {
                yield return $"{label} x({name}): {p:R} vs {q:R}";
            }
        }
    }

    /// <summary>The transport figures of two stations within a relative tolerance on the doubles and exactly on the counts and statuses.</summary>
    public static IEnumerable<string> TransportDifferences(Station expected, Station actual, double relative, string label)
    {
        if (expected.TransportStatus != actual.TransportStatus || expected.Transport.HasValue != actual.Transport.HasValue)
        {
            yield return $"{label}: transport statuses differ";
            yield break;
        }

        if (expected.Transport is not { } a || actual.Transport is not { } b)
        {
            yield break;
        }

        foreach (var field in typeof(TransportFigures).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var x = field.GetValue(a)!;
            var y = field.GetValue(b)!;
            var same = x is double p && y is double q ? Math.Abs(p - q) <= relative * Math.Max(Math.Abs(p), Math.Abs(q)) : x.Equals(y);
            if (!same)
            {
                yield return $"{label} transport {field.Name}: {x} vs {y}";
            }
        }
    }
}
