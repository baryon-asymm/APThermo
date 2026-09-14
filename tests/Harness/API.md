# API.md — Harness

Namespace `AerospacePropellantThermodynamics.Harness`. The scaffolding the test nodes
share: a CPU host, bit comparison, bit hashes with their approval files, fixture
families. Everything not listed here is internal and may change.

## Host ⏳

```csharp
namespace AerospacePropellantThermodynamics.Harness;

public sealed class CpuHost : IDisposable                  // one per test assembly, held by the consumer's collection or class fixture
{
    public CpuHost();                                       // one context, one CPU accelerator, the committed database with trans.inp, the tolerance table
    public Context Context { get; }
    public Accelerator Accelerator { get; }
    public SpeciesDatabase Database { get; }
    public ToleranceTable Tolerances { get; }
    public void Dispose();
}
```

## Bits ⏳

```csharp
public static class Bits
{
    public static bool Same(double expected, double actual);                  // DoubleToInt64Bits equal
    public static IEnumerable<string> Differences<T>(T expected, T actual, string label) where T : struct;   // one line per public double or int field whose bits differ: "label.Field: expected E, actual A", round-trip form
}

public sealed class BitHash                                 // SHA-256 over little-endian bytes, in the order added
{
    public BitHash Add(double value);
    public BitHash Add(ReadOnlySpan<double> values);
    public BitHash Add(int value);
    public BitHash Add(ReadOnlySpan<int> values);
    public BitHash Add(string text);                        // its UTF-8 bytes, as the recorded snapshots hash a string
    public string ToHex();                                  // lowercase hexadecimal
}

public sealed class ApprovedSnapshot                        // a snapshot file of "key hash" lines
{
    public static ApprovedSnapshot Load(string approvedPath);                  // an absent file is an empty snapshot
    public string? Problem(string key, string actualLine);                     // null when the approved line of the key equals it; else the problem naming the key and how to approve, the actual line kept for the actual file
    public IReadOnlyList<string> StaleKeys(IEnumerable<string> producedKeys);  // approved keys no run produced
}
```

## Fixture families ⏳

```csharp
public static class FixtureFamilies
{
    public static IEnumerable<object[]> Of(IEnumerable<string> kinds, Func<CeaCase, string> key);   // theory data: the cases of the kinds grouped by the key, largest family first, ties ordinal by key; each row [key, count, the cases]
}
```

The signatures above are a sketch drawn from the copies the node replaces; the commit
that creates the code writes the real ones here under ✅ (`AGENTS.md` §7).

## Errors

| Situation | Behaviour |
|---|---|
| the database or the tolerance table cannot be loaded | the `Data` or `Fixtures` exception, from the `CpuHost` constructor |
| an approved snapshot file whose lines are not "key hash" | `FormatException` naming the file and the line |

## Side effects

`CpuHost` creates an ILGPU context and a CPU accelerator and reads the data files and
the tolerance table; `ApprovedSnapshot` writes `*.actual.txt` next to an approved file
when it finds a problem.

## Out of scope

- Any comparison within a tolerance, any formula, any type of the nodes under test.
- CUDA: the execution tests node creates its own accelerators.
