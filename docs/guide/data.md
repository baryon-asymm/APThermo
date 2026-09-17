# The database

## Purpose

The NASA thermodynamic (`thermo.inp`) and transport (`trans.inp`) database APThermo
reads: how it is embedded in the package, how to load your own files instead, and how
to read a run's database provenance and the NASA attribution.

## When to use

- You want to confirm which database bytes a run actually used (the SHA-256 hashes in
  its provenance).
- You have an updated or a different NASA file set and want to load it instead of the
  bundled one.
- You need the Apache-2.0 attribution text for the embedded data, at run time or in
  the repository.

## Steps

1. **The default: no files needed.** `SpeciesDatabase.LoadBundled()` reads the NASA
   files embedded in the `APThermo` assembly — the same bytes as the repository's
   `data/thermo.inp` and `data/trans.inp`, hashed the same way, so `Provenance`
   carries the same hashes either way. Every guide example on this site uses it.
2. **Loading your own files instead.** `SpeciesDatabase.Load(thermoPath, transPath)`
   reads files from a path you give; `transPath` is optional (transport properties
   are then unavailable). This example reads the repository's own committed files:

<!-- snippet: DatabaseFromFilesUsings -->
```csharp
using APThermo.Data;
```

`output` is any `TextWriter` (`Console.Out` in a console application), and `directory`
is a folder holding `thermo.inp` and, optionally, `trans.inp`:

<!-- snippet: DatabaseFromFiles -->
```csharp
var database = SpeciesDatabase.Load(
    Path.Combine(directory, "thermo.inp"),
    Path.Combine(directory, "trans.inp"));

output.WriteLine($"products = {database.Products.Count}, reactants = {database.Reactants.Count}");
output.WriteLine($"thermoSha256 starts with: {database.Provenance.ThermoSha256[..12]}");
```

3. **From the command line**: `--database DIR` names a directory with `thermo.inp`
   and, optionally, `trans.inp`; without it the tool uses the embedded database.
   Either way, the output document's `run.database` carries `thermoSha256` and
   `transSha256`; with `--database` it also carries the real file paths, and without
   it the markers `"embedded:thermo.inp"` and `"embedded:trans.inp"`.
4. **Provenance and licensing.** `database.Provenance.ThermoSha256` and
   `.TransSha256` are lowercase hex SHA-256 of the exact bytes read.
   `SpeciesDatabase.BundledNotice()` returns the Apache-2.0 `NOTICE` text embedded
   with the data, for a consumer who has only the package, not the repository; the
   same text is committed at `data/NOTICE`.

## Errors

- **`--database DIR` or a path given to `Load` does not exist**: `FileNotFoundException`
  before anything is parsed.
- **A malformed file** (a truncated record, a count that does not match the lines
  present): `DatabaseFormatException` naming the file and the line number; nothing is
  returned.
- **An unknown species name**: the indexer throws `KeyNotFoundException`; `TryGet`
  returns `false` and `Records` an empty list instead, for callers that want to check
  first.
- **`AtomicWeight` of an element with no monatomic gaseous record**:
  `KeyNotFoundException`.
- **Transport requested on a database loaded without `trans.inp`**: refused at
  `Solve`, an `ArgumentException` naming the missing file.

## See also

- [GPU acceleration](gpu.md) — choosing the accelerator that reads this database
- [Command line](cli.md) — `--database`, `apthermo species`
- [Troubleshooting](troubleshooting.md) — every exit code and status in one place
- [API.md](../../src/Data/API.md) — `SpeciesDatabase`, `DatabaseProvenance`, every field of a species record
