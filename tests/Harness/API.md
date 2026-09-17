# API.md — Harness

Namespace `APThermo.Harness`. The scaffolding the test nodes
share: a CPU host, bit comparison, bit hashes with their approval files, fixture
families, and the JSON-document helpers (schema validation, the `run`-property cut).
Everything not listed here is internal and may change.

## Host ✅

```csharp
namespace APThermo.Harness;

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

## Bits ✅

```csharp
public static class Bits
{
    public static bool Same(double expected, double actual);                  // DoubleToInt64Bits equal
    public static IEnumerable<string> Differences<T>(T expected, T actual, string label) where T : struct;   // one line per public field whose value differs: doubles by their bits, any other field by Equals
}

public sealed class BitHash                                 // SHA-256 over little-endian bytes, in the order added; one instance computes one digest
{
    public BitHash Add(double value);
    public BitHash Add(ReadOnlySpan<double> values);
    public BitHash Add(int value);
    public BitHash Add(ReadOnlySpan<int> values);
    public BitHash Add(bool value);                         // one byte, 1 or 0, as BinaryWriter.Write(bool) writes it
    public BitHash Add(string text);                        // its UTF-8 bytes, as the recorded snapshots hash a string
    public string ToHex();                                  // lowercase hexadecimal; releases the underlying algorithm
}

public sealed class ApprovedSnapshot                        // a snapshot file of "key value" lines: tab-delimited when a recorded key can hold a space, space-delimited otherwise, detected from the file itself
{
    public static ApprovedSnapshot Load(string approvedPath);                  // an absent file is an empty snapshot
    public string? Problem(string key, string actualLine);                     // null when the approved line of the key equals it; else the problem naming the key and how to approve; the pair is kept, and the actual file is (re)written beside the approved one from this call on
    public IReadOnlyList<string> StaleKeys(IEnumerable<string> producedKeys);  // approved keys no run produced, sorted ordinally
}
```

## Fixture families ✅

```csharp
public static class FixtureFamilies
{
    public static IEnumerable<object[]> Of(IEnumerable<string> kinds, Func<CeaCase, string> key);   // theory data: the cases of the kinds grouped by the key, largest family first, ties ordinal by key; each row [key, count, cases] with cases an IReadOnlyList<CeaCase>
}
```

## JSON documents ✅

The JSON-document helpers the test nodes that check documents share; they move here
from `Cli.Tests` in the distribution phase (2026-09-16), where their second consumer,
the docs tests node, belongs.

```csharp
public sealed class JsonSchema        // the part of JSON Schema the tree's schema files use: type, enum, const, properties, required, additionalProperties, items, minItems, minimum, exclusiveMinimum, oneOf, anyOf and local $ref into $defs; a keyword outside this list is an error of validation
{
    public static JsonSchema Load(string path);                       // from a file
    public static JsonSchema Parse(string text);                      // from the schema's JSON text, as `apthermo schema` prints it (2026-09-16)
    public IReadOnlyList<string> Validate(JsonElement instance);      // every violation with its JSON path; empty when the instance conforms
}

public static class RunPropertyCut
{
    public static byte[] Bytes(byte[] document, string example);      // the object's bytes with its top-level `run` property cut out; throws naming the example when there is no such property or more than one
}
```

⚠ 2026-09-16: born pending in the distribution phase; the types were internal in
`Cli.Tests` and moved here with the `Parse` entry point added. The move is done — both are
public in `APThermo.Harness` now, so this section is checked against the code.

## Errors

| Situation | Behaviour |
|---|---|
| the database or the tolerance table cannot be loaded | the `Data` or `Fixtures` exception, from the `CpuHost` constructor |

## Side effects

`CpuHost` creates an ILGPU context and a CPU accelerator and reads the data files and
the tolerance table; `ApprovedSnapshot` writes `*.actual.txt` next to an approved file
from the first problem a run finds in it on, so that the file always ends complete
through the last key the caller passed it.

## Out of scope

- Any comparison within a tolerance, any formula, any type of the nodes under test.
- CUDA: the execution tests node creates its own accelerators.
