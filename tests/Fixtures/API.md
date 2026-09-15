# API.md — Fixtures

Namespace `APThermo.Fixtures`. The node exposes the reference
cases as documents, a loader, the tolerance table, and the generator scripts.
Everything not listed here is internal and may change.

## Repository paths ✅

```csharp
namespace APThermo.Fixtures;

public static class RepositoryPaths
{
    public static string Root { get; }                              // the directory holding AGENTS.md, found upward from this node's source file
    public static string Data { get; }                              // <Root>/data
    public static string Resolve(params string[] segments);         // Path.Combine(Root, segments)
}

public static class FixtureFiles
{
    public static string Root { get; }                              // tests/Fixtures/cases, from the repository root
    public static IReadOnlyList<string> Enumerate(string kind);     // file paths of cases/<kind>/*.json, sorted ordinally; DirectoryNotFoundException for an unknown kind
}
```

## Loader ✅

```csharp
public sealed record Provenance(
    string Package, string Version, string LibraryVersion,           // "cea", "3.3.4", the library's version string
    string Method,                                                   // "cea-package" or "independent-evaluation"
    string Script, string ScriptSha256,
    string ThermoLibSha256, string TransLibSha256,                   // the package's data libraries
    string DataThermoSha256, string DataTransSha256,                 // the tree's data/ files
    DateOnly GeneratedOn);                                           // the day the content last changed

public sealed record CeaCase(
    string Name, string Kind,
    JsonElement Inputs,                                              // by name, SI
    JsonElement Outputs,                                             // by name, SI
    Provenance Generator,
    string Path);                                                    // the file it was read from

public static class CeaFixtures
{
    public static CeaCase Load(string path);
    public static IReadOnlyList<CeaCase> LoadAll(string kind);      // FixtureFiles.Enumerate(kind), each loaded
}

public readonly record struct Tolerance(double Absolute, double Relative);

public sealed class ToleranceTable
{
    public static string Path { get; }                               // tests/Fixtures/tolerances.json
    public static ToleranceTable Load();
    public static ToleranceTable Load(string path);
    public IReadOnlyList<string> Fields { get; }                     // sorted ordinally
    public Tolerance For(string field);                              // KeyNotFoundException for an unknown field
    public string Derivation(string field);
    public bool Matches(string field, double expected, double actual); // |expected − actual| ≤ Absolute + Relative·|expected|
    public string MoleFractionField(double referenceValue);          // "moleFraction" when referenceValue is not below For("moleFraction").Absolute, else "moleFractionTrace"
}

public sealed class FixtureFormatException : Exception
{
    public FixtureFormatException(string fileName, string field, string message);
    public string FileName { get; }
    public string Field { get; }                                     // "case.kind", "generator.version", "outputs", …
}
```

`Load` checks the form of a document: `case.name`, `case.kind`, `case.inputs`, every
provenance field, `outputs`, the date format, and that `case.kind` equals the name of
the directory the file lies in. It does not check the outputs of a kind: which fields
a kind carries, and the caveats of the reference's fields (`mixtureMolarMass`, the
frozen-station `cv`, the frozen `cp` of the transport set when transport is on, the
reacting conductivity at the trace-component stations), are described in `BOOT.md`
and used by the test nodes.

## Generator ✅

```console
$ python -m venv tests/Fixtures/generate/.venv
$ tests/Fixtures/generate/.venv/Scripts/pip install -r tests/Fixtures/generate/requirements.txt
$ tests/Fixtures/generate/.venv/Scripts/python tests/Fixtures/generate/regenerate.py            # writes changed fixtures, removes stale ones
$ tests/Fixtures/generate/.venv/Scripts/python tests/Fixtures/generate/regenerate.py --check    # compares only; exit 1 on any difference
$ tests/Fixtures/generate/.venv/Scripts/python tests/Fixtures/generate/regenerate.py thermo tp  # only these kinds
```

The scripts form the child node [generate](./generate/API.md).
Scripts: `rp1311.py` (the six RP-1311 examples and the cases derived from examples 8
and 12), `propellants.py` (the four reference propellants and the derived equilibrium
cases), `thermo_functions.py` (independent polynomial evaluation), `transport_fits.py`
(independent fit evaluation), `constants.py`; `cea_cases.py` holds the case builders
over the package, `common.py` the paths, the generator's own reader of the NASA files,
element moles and unit factors; all write through one `writer.py` that adds the
provenance and does the comparison. Each script runs standalone with the same options.

## Errors

| Situation | Behaviour |
|---|---|
| a fixture file is not JSON, lacks a field of the document form, or names a kind other than its directory | `FixtureFormatException` naming the file and the field |
| a tolerance entry lacks a derivation or has a negative tolerance | `FixtureFormatException` naming the file and the field |
| an unknown field in `ToleranceTable.For`, `Derivation` or `Matches` | `KeyNotFoundException` naming the field |
| an unknown kind in `FixtureFiles.Enumerate` or `CeaFixtures.LoadAll` | `DirectoryNotFoundException` |

## Side effects

The loader reads files. The generator writes into `cases/` only.

## Out of scope

- Running the tree's own code: this node knows nothing of it.
- Deciding whether a difference is acceptable beyond the tolerance table: the test nodes.
