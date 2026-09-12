# API.md — Fixtures

Namespace `AerospacePropellantThermodynamics.Fixtures`. The node exposes the reference
cases as documents, a loader, the tolerance table, and the generator scripts.
Everything not listed here is internal and may change.

## Loader ⏳

```csharp
namespace AerospacePropellantThermodynamics.Fixtures;

public static class FixtureFiles
{
    public static string Root { get; }                              // tests/Fixtures/cases, from the repository root
    public static IReadOnlyList<string> Enumerate(string kind);     // file paths, sorted
}

public sealed record Provenance(
    string Package, string Version, string LibraryVersion,
    string Script, string ScriptSha256,
    string ThermoLibSha256, string TransLibSha256,
    string DataThermoSha256, string DataTransSha256,
    DateOnly GeneratedOn);

public sealed record CeaCase(
    string Name, string Kind,
    JsonElement Inputs,                                              // by name, SI
    JsonElement Outputs,                                             // by name, SI
    Provenance Generator);

public static class CeaFixtures
{
    public static CeaCase Load(string path);
    public static IReadOnlyList<CeaCase> LoadAll(string kind);
}

public readonly record struct Tolerance(double Absolute, double Relative);

public sealed class ToleranceTable
{
    public static ToleranceTable Load();                             // tests/Fixtures/tolerances.json
    public Tolerance For(string field);                              // throws for an unknown field
    public bool Matches(string field, double expected, double actual);
}
```

## Generator ⏳

```console
$ python -m venv .venv && .venv/Scripts/pip install -r tests/Fixtures/generate/requirements.txt
$ python tests/Fixtures/generate/regenerate.py            # writes cases/, exits 1 when a committed fixture differs
$ python tests/Fixtures/generate/regenerate.py --check    # compares only
```

Scripts: `rp1311.py` (the six RP-1311 examples), `propellants.py` (the four
reference propellants and the derived equilibrium cases), `thermo_functions.py`
(independent polynomial evaluation), `transport_fits.py`, `constants.py`; all write
through one `writer.py` that adds the provenance.

## Errors

| Situation | Behaviour |
|---|---|
| a fixture file is malformed or lacks a field of its kind | `FixtureFormatException` naming the file and the field |
| an unknown field in `ToleranceTable.For` | `KeyNotFoundException` |

## Side effects

The loader reads files. The generator writes into `cases/` only.

## Out of scope

- Running the tree's own code: this node knows nothing of it.
- Deciding whether a difference is acceptable beyond the tolerance table: the test nodes.
