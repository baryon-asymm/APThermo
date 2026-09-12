# BOOT.md — Cli

## Purpose

The command-line front end `apthermo`: reads a problem from a JSON file, runs it
through `Problems`, and writes the results as JSON or CSV; also lists the database
and the accelerators. It is a thin adapter kept apart so that the library never
depends on console, serialization or file-layout concerns.

## Invariants

- **The JSON schema is the contract.** Input and output documents follow the schema
  in `API.md`; every field of the result records reaches the output document with the
  same name and unit as in the library, plus the presentation conveniences listed
  there (specific impulse in seconds, a mole-fraction threshold).
- **Units in documents are SI** unless a field name carries the unit explicitly
  (`specificImpulseSeconds`); the conversion to seconds uses g0 = 9.80665 m/s² and
  happens only here.
- **Exit codes mean something**: 0 every case `Ok`; 1 at least one case failed
  numerically (the document is still written); 2 invalid input document; 3 accelerator
  or infrastructure error. Messages go to standard error; documents go to the output
  file or standard output.
- **No hidden state**: no configuration files, no registry, no environment variable
  except the ones `Execution` reads.

## Dependencies

- [Problems](../Problems/API.md) — propellants, problems, the solver, result records.
- [Data](../Data/API.md) — loading the database and listing species.

Outside the tree: the .NET base class library (`System.Text.Json`); command-line
parsing is hand-written to avoid a dependency (revisited if the surface grows).

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Commands: `rocket <input.json>`, `equilibrium <input.json>`, `states <records>`,
  `species [--find TEXT]`, `devices`; options `--output PATH`, `--format json|csv`, `--accelerator auto|cpu|cuda`,
  `--database DIR` (directory with `thermo.inp` and `trans.inp`; default `data/` next
  to the executable, then the current directory), `--threshold X` (mole fractions below
  X are omitted from the composition tables; default 5e-6, the reference's print threshold).
- CSV output flattens one row per case and station with the performance figures of
  the station's exit; compositions are not in CSV.
- Sweeps in the input document expand to one batch (Cartesian product), so the
  command line is the natural way to use the GPU.
- `states` reads records in the exchange shape of the `Problems` node (`pressure`,
  `composition`, one of `enthalpy`/`temperature`/`entropy`, optional exits) from a JSON
  array, a JSON Lines file or several files, and writes one result per record in the
  input order, with the record echoed under `inputs`; a record that fails keeps its
  place with its status.
- Timings and the accelerator description are always in the output document's
  `run` section.

## Acceptance criteria

- [ ] Every example document in `API.md` runs end to end against the committed data
      files and produces a document that validates against the output schema (schema
      files kept next to the tests).
- [ ] The example rocket document for LOX/LH2 gives the same numbers as the library
      call in the front door tests (the CLI test compares its output document with the
      library result, field by field over a reflection-generated list).
- [ ] Exit codes 0, 1, 2, 3 are each produced by a test (a good document, a document
      with a failing case, a malformed document, `--accelerator cuda` with
      `APTHERMO_NO_CUDA=1`).
- [ ] CSV output has one row per case and station and the documented columns
      (checked against a golden CSV).

## Taboos

- No physics, no unit conversion beyond seconds for specific impulse, no defaults
  for missing physical inputs.
- No output format other than JSON and CSV in version 1; a CEA-like text table is a
  separate decision.
- No network, no telemetry.
