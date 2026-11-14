# API.md — Harness

Namespace `APThermo.Harness`. The scaffolding the test nodes
share: a CPU host, bit comparison, bit hashes with their approval files, fixture
families, the JSON-document helpers (schema validation, the `run`-property cut, the
field-by-field comparison), and the runner of a built assembly as a process.
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
    public static IEnumerable<string> Differences<T>(T expected, T actual, string label) where T : struct;   // one line per public double or int field or property whose value differs: doubles by their bits, ints by Equals; throws when T has none
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
    public static string RecordPathFor(string directory, string baseName);      // <baseName>.approved.txt on every platform (2026-10-05): the one place that names a node's bit record
    public static string ApprovedPathFor(string directory, string baseName);    // <baseName>.approved.txt, or <baseName>.linux.approved.txt on Linux: the throughput figures, which stay per platform
    public static ApprovedSnapshot Load(string approvedPath);                  // an absent file is an empty snapshot
    public string? Problem(string key, string actualLine, IReadOnlyList<string>? fields = null);   // null when the approved line of the key equals it; else the problem naming the key and how to approve; the pair is kept, and the actual file is (re)written beside the approved one from this call on; fields given and the key a problem, they are written one per line to a per-case dump beside the actual file (<actual-base>.<sanitized key>.fields.txt)
    public IReadOnlyList<string> StaleKeys(IEnumerable<string> producedKeys);  // approved keys no run produced, sorted ordinally
}
```

## Fixture families ✅

```csharp
public sealed record FixtureFamilyKey(string Key, int Count);   // one family's key and case count, as Keys reports it

public static class FixtureFamilies
{
    public static IReadOnlyList<FixtureFamilyKey> Keys(IEnumerable<string> kinds, Func<CeaCase, string> key);   // the key and case count of every family, largest first, ties ordinal by key; a caller builds its own TheoryData from this
    public static IReadOnlyList<CeaCase> CasesOf(IEnumerable<string> kinds, Func<CeaCase, string> key, string familyKey);   // the cases of the named family, grouped the same way Keys did
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

The field-by-field comparison of two documents (2026-09-30), moved here from the docs
tests node so that the workflow step running the packed tool's example holds the same
rule as the docs tests, once.

```csharp
public static class JsonFieldComparison
{
    public const double RelativeNumberTolerance = 1e-9;                          // four orders above the measured platform difference (about 1e-13), five below the fixtures' tolerance rows
    public static string? Mismatch(JsonElement approved, JsonElement actual, string path);   // null when the same members in the same order, every string and boolean equal, every number within RelativeNumberTolerance relative to the larger magnitude (exactly equal at zero); else the first difference found depth-first, named by its JSON path from `path` ("$" for the whole document); throws ArgumentOutOfRangeException for a JsonValueKind that does not exist
}
```

`Parse` audits every schema box before returning, including definitions and branches
that a particular instance never visits. Unknown keywords, invalid local references
and reference cycles fail with `InvalidOperationException`; the supported subset
has a finite reference graph. Boolean schemas are accepted. Objects carried by
`enum`/`const` and annotation values are data, not schema boxes. A `$ref` combines
with its sibling constraints as in the tree's draft-2020-12 schemas; JSON Pointer
segments decode `~1` and `~0`.

⚠ 2026-11-14: a `$ref` previously skipped its siblings and keyword checks occurred
only during validation of visited boxes (audit A17). The up-front audit and
conjunctive sibling validation replace those incomplete checks.

## Processes ✅

A built .NET assembly run as a separate process (2026-10-03, the test pyramid: the process runner
of `Cli.Tests`, moved here so that `Docs.Tests` runs the command line as a process the same way).

```csharp
public sealed record DotnetProcessResult(int ExitCode, ReadOnlyMemory<byte> Output, ReadOnlyMemory<byte> Error);   // the raw bytes of both standard streams, in no encoding

public static class DotnetProcess
{
    public static DotnetProcessResult Run(string assemblyPath, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout, IReadOnlyDictionary<string, string>? environment = null, CancellationToken cancellationToken = default);
    public static DotnetProcessResult Run(string assemblyPath, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string>? environment = null);   // `dotnet <assemblyPath> <arguments>` in workingDirectory, environment set on top of the inherited one, waited for
    public static string BuiltAssemblyPath(string assemblyName, params string[] projectDirectory);   // the .dll next to the running test assembly when its runtime configuration was copied there, else the project's build output (projectDirectory below the repository root, e.g. "src", "Cli") in the running configuration
}
```

The arguments are passed one to one, never re-parsed. Standard output and standard error are read as bytes from
the process's base streams, so a caller sees the encoding the process wrote on the platform it ran on and
decodes it itself. The type names no type of the node under test: the assembly is a path.

## Errors

| Situation | Behaviour |
|---|---|
| `dotnet` does not start | native `Process.Start` exception (normally `Win32Exception`); `InvalidOperationException` if no process was returned |
| `BuiltAssemblyPath` finds no built assembly | `FileNotFoundException` naming the path it looked at |
| the database or the tolerance table cannot be loaded | the `Data` or `Fixtures` exception, from the `CpuHost` constructor |
| `Bits.Differences<T>` is called on a `T` with no public `double` or `int` field or property | `InvalidOperationException`, naming the type |
| a schema uses a keyword outside `Validate`'s list | `InvalidOperationException`, naming the keyword |
| a schema reference is not a local URI-fragment JSON Pointer, has invalid percent/UTF-8/~ escapes, or does not resolve | `InvalidOperationException`, naming the reference |
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
folded them (BOOT.md, the per-case field dump). Both are git-ignored. `DotnetProcess.Run` starts a
`dotnet` process and waits for it; it writes no file of its own, and the process it starts may.

## Out of scope

- Any comparison within a tolerance, any formula, any type of the nodes under test.
- CUDA: the execution tests node creates its own accelerators.

## Snapshot path safety ✅

A conventional `<name>.approved.<extension>` writes `<name>.actual.<extension>`.
For an arbitrary input file name, `.actual` is inserted before the extension, so
`baseline.txt` writes `baseline.actual.txt`. Paths are captured as full paths.
Actual records and caller-opted field dumps replace their directory entries
atomically; an existing output link is not followed into the approved content.
Approved bytes are never rewritten by this node.

⚠ 2026-11-14: the old substring replacement could leave the path unchanged when
`approved` was absent or differently cased (audit A16), writing over the baseline.
The explicit fallback path removes that hidden file-name precondition.

## Process deadlines ✅

The original Run overload uses five minutes. The timeout overload requires a positive
TimeSpan no greater than uint.MaxValue-1 milliseconds. An already cancelled token
prevents launch. Expiry throws TimeoutException; cancellation throws
OperationCanceledException with the caller token. Interrupted calls attempt a tree
kill and spend at most another five seconds draining/reaping, then close pipe readers.
Both exceptions attach partial raw byte[] under Data["StandardOutput"] and
Data["StandardError"], and bounded readable excerpts in their message. Excerpts JSON-escape control bytes;
the attached raw arrays remain unchanged.

⚠ 2026-11-14: process/pipe waits were formerly unbounded. The additive overload and
default deadline retain byte/argument/environment behavior while making hangs visible.

⚠ 2026-11-14: the initially proposed timeout overload placed cancellation before
optional environment. CA1068 rejected that order; the owner corrected it to keep
the cancellation token last. The original four-argument overload remains unchanged.

Pre-launch cancellation also attaches empty StandardOutput/StandardError arrays.

⚠ 2026-11-14: the old start-failure row/XML description implied only
InvalidOperationException. Native Process.Start exceptions already propagate; that
exception is explicitly created only for a null returned process. The table and XML
now distinguish those cases. No exception wrapper or normalization was introduced.

JSON field comparison requires finite converted double output values. An invalid or
nonfinite numeric result is a path-named mismatch before tolerance arithmetic; the
relative number tolerance is unchanged (IT01, 2026-11-14).

Schema numeric validation is exact mathematical JSON decimal validation: integer,
minimum/exclusiveMinimum, minItems and numeric enum/const equality do not round to
double/decimal or expand enormous exponent-sized powers. Supported valid numeric
forms include fractional spellings of integers and exponents outside machine ranges.
The keyword subset is unchanged (IT02, 2026-11-14).

Local schema references percent-decode exactly once as strict UTF-8 before pointer
token splitting and ~0/~1 decoding. Raw fragment characters follow RFC 3986; literal
reserved/Unicode characters use their valid percent-encoded forms. Plus remains plus,
encoded percent is not decoded again, and Unicode is not normalized. Traversal stays
object-only; remote/anchor references and cyclic schemas remain unsupported (IT04).
