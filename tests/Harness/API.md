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
    public string ToHex();                                  // lowercase hexadecimal; releases the underlying algorithm; Fields stays readable
    public IReadOnlyList<string> Fields { get; }             // every value added so far, as text in the order added (a double round-trip "R", invariant culture); not what is hashed, a caller's opt-in into the per-case field dump below
}

public sealed class ApprovedSnapshot                        // a snapshot file of "key value" lines: tab-delimited when a recorded key can hold a space, space-delimited otherwise, detected from the file itself
{
    public static string ApprovedPathFor(string directory, string baseName);   // <baseName>.approved.txt, or <baseName>.linux.approved.txt on Linux; the one place that picks between a node's per-platform bit snapshots
    public static ApprovedSnapshot Load(string approvedPath);                  // an absent file is an empty snapshot
    public string? Problem(string key, string actualLine, IReadOnlyList<string>? fields = null);   // null when the approved line of the key equals it; else the problem naming the key and how to approve; the pair is kept, and the actual file is (re)written beside the approved one from this call on; fields given and the key a problem, they are written one per line to a per-case dump beside the actual file (<actual-base>.<sanitized key>.fields.txt)
    public IReadOnlyList<string> StaleKeys(IEnumerable<string> producedKeys);  // approved keys no run produced, sorted ordinally
}
```

## Fixture families ✅

```csharp
public static class FixtureFamilies
{
    public static TheoryData<string, int, IReadOnlyList<CeaCase>> Of(IEnumerable<string> kinds, Func<CeaCase, string> key);   // theory data: the cases of the kinds grouped by the key, largest family first, ties ordinal by key; each row (key, count, cases)
}
```

## JSON documents ✅

The JSON-document helpers the test nodes that check documents share; they move here
from `Cli.Tests` in the distribution phase (2026-09-16), where their second consumer,
the docs tests node, belongs.

```csharp
public sealed class JsonSchema        // the part of JSON Schema the tree's schema files use: type, enum, const, properties, required, additionalProperties, items, minItems, minimum, exclusiveMinimum, oneOf, anyOf and local $ref into $defs
{
    public static JsonSchema Parse(string text);                      // from the schema's JSON text, as `apthermo schema` prints it (2026-09-16)
    public IReadOnlyList<string> Validate(JsonElement instance);      // every violation with its JSON path; empty when the instance conforms
}

public static class RunPropertyCut
{
    public static byte[] Bytes(byte[] document, string example);      // the object's bytes with its top-level `run` property cut out; throws naming the example when there is no such property, more than one, or the document is not shaped as this method expects
}
```

## Errors

| Situation | Behaviour |
|---|---|
| the database or the tolerance table cannot be loaded | the `Data` or `Fixtures` exception, from the `CpuHost` constructor |
| a schema uses a keyword outside `Validate`'s list | `InvalidOperationException`, naming the keyword |
| a schema's `$ref` is not local (does not start with `#/`), or does not resolve | `InvalidOperationException`, naming the reference |
| a schema names a `type` `Validate` does not know | `InvalidOperationException`, naming the type |
| the document `RunPropertyCut.Bytes` is given is not a JSON object at the top level, or has a malformed top-level property | `InvalidOperationException`, naming the example |
| the document has no top-level `run` property, more than one, or only that one property | `InvalidOperationException`, naming the example |

## Side effects

`CpuHost` creates an ILGPU context and a CPU accelerator and reads the data files and
the tolerance table; `ApprovedSnapshot` writes `*.actual.txt` next to an approved file
from the first problem a run finds in it on, so that the file always ends complete
through the last key the caller passed it; when a caller also passes `Problem` a
`BitHash`'s `Fields`, a problem on that key writes one further file beside it,
`<actual file>.<sanitized key>.fields.txt`, one field per line in the order the hash
folded them (BOOT.md, the per-case field dump). Both are git-ignored.

## Out of scope

- Any comparison within a tolerance, any formula, any type of the nodes under test.
- CUDA: the execution tests node creates its own accelerators.
