# Command line

## Purpose

Use the `apthermo` command-line tool to solve rocket, equilibrium and state-record
problems from JSON documents, list database species, inspect available accelerators,
and print the JSON Schemas of every document shape it reads and writes.

## When to use

- Scripting: you want to drive APThermo from a shell script or CI pipeline without
  writing C#.
- Batch processing: you have problem documents already, or a sweep is easier to
  express as a document than as C# code.
- Inspection: you want to see the database species, the available accelerators, or
  the schema of a document shape, without writing any code.

## Steps

1. Install the tool globally:

```
dotnet tool install --global APThermo.Cli
```

2. Solve a rocket problem from a JSON document shaped as
   [the input document](../../src/Cli/API.md#input-document-). Save this as
   `samples/cli/problems/rocket.json` (already there in a clone of the repository);
   `--accelerator cpu` pins the accelerator so the run is reproducible:

<!-- cli-document: problems/rocket.json -->
```json
{
  "propellant": {
    "reactants": [
      { "name": "O2(L)", "role": "oxidizer", "amount": 1.0, "temperature": 90.17 },
      { "name": "H2(L)", "role": "fuel", "amount": 1.0, "temperature": 20.27 }
    ],
    "mixture": { "oxidizerToFuel": 6.0 }
  },
  "problem": {
    "type": "rocket",
    "chamberPressure": 7000000.0,
    "flow": "shifting-equilibrium",
    "areaRatios": [20.0, 77.5],
    "transport": true
  }
}
```

```console
$ apthermo rocket samples/cli/problems/rocket.json --accelerator cpu
```

3. Solve a single equilibrium state (tp, hp or sp) instead of a nozzle:

```console
$ apthermo equilibrium samples/cli/problems/equilibrium.json --accelerator cpu
```

4. Solve state records from another simulation — an array, a single object, or JSON
   Lines, one record per line. `--transport` requests transport properties for a
   `states` run (the `rocket` and `equilibrium` commands ask for it through the
   document's own `problem.transport` field instead):

```console
$ apthermo states samples/cli/states/tp-states.json --accelerator cpu
```

5. The JSON result document is written to standard output; redirect it to a file, or
   pass `--output result.json`. `--format csv` writes the flattened form instead. See
   [the output document](../../src/Cli/API.md#output-document-) for its shape.

6. A rocket or equilibrium document may also carry a `sweep` object next to
   `problem`: a list, or a `{from, to, step}` range, per varying field
   (`oxidizerToFuel` and `chamberPressure` for a rocket problem; `oxidizerToFuel`,
   `pressure` and, for a tp problem, `temperature` for an equilibrium problem). The
   command solves the product of every list as one batch, without a separate CLI
   option; see [Batch solving](batch.md) and
   [the input document](../../src/Cli/API.md#input-document-) for the exact shape.

Other commands, none of which take an input document:

```console
$ apthermo species --find H2O
```

```console
$ apthermo devices
```

```console
$ apthermo schema input
```

`species` searches the database (`--find` narrows it, `--database DIR` reads a
different one, `--format csv` writes the flattened form); `devices` lists the CPU and
CUDA accelerators this machine offers, with `cudaSkippedBecause` when CUDA was tried
and skipped (see [GPU acceleration](gpu.md)); `schema` prints one of the five embedded
JSON Schemas (`input`, `output`, `states`, `species`, `devices`) by name to standard
output or to `--output`, or refuses naming every embedded name.

Every command that writes a document (every solving command, `species`, `devices` and
`schema`) takes `--output PATH`; `species` and the solving commands also take
`--format json|csv`. Every solving command takes `--database DIR` (a directory with
`thermo.inp` and, optionally, `trans.inp`; without it the tool uses the database
embedded in `APThermo`, see [The database](data.md)), `--threshold X` (the
mole-fraction print cutoff, default `5e-6`) and `--mass-tolerance X` (the relative
mass tolerance declared for every mixture the command builds from element moles,
default `1e-2`; see [Troubleshooting](troubleshooting.md)). The full option list, with
every command's exact synopsis, is
[the command line's `API.md`](../../src/Cli/API.md#command-line-); it is not repeated
here as a runnable form because most of its options are placeholders you fill in, not
literal text to paste.

## Errors

Exit codes:

| Code | Meaning |
|---|---|
| 0 | All cases solved successfully. |
| 1 | At least one case or station failed numerically; the document is still written with a per-case and per-station status. |
| 2 | Invalid input document, unknown option, bad database path, unknown reactant, mass-tolerance refusal, or unknown schema name. A message on standard error names the problem. |
| 3 | Accelerator unavailable (no CUDA driver, ILGPU mismatch) or an unexpected infrastructure failure. Every path tried is named. |

Common exit-code-2 situations: a JSON field with the wrong type or an unknown field, a
species name not in the database, a state record with none or more than one of
`temperature`, `enthalpy` and `entropy`, a composition whose element moles do not
weigh one kilogram within the tolerance in force, a `--database` path that does not
exist. See [Troubleshooting](troubleshooting.md) for the exit codes and every
`CaseStatus` in one place, and [the command line's `API.md`](../../src/Cli/API.md#errors)
for the exact situation-by-situation table with an example message.

## See also

- [Getting started](getting-started.md) — install and solve the first case
- [Rocket solving](rocket.md) — the C# API behind the `rocket` command
- [Equilibrium states](equilibrium.md) — the C# API behind the `equilibrium` command
- [State records](states.md) — the C# API behind the `states` command
- [GPU acceleration](gpu.md) — `--accelerator`, `devices`, and CUDA discovery
- [The database](data.md) — `--database`, provenance, and the bundled NASA files
- [Troubleshooting](troubleshooting.md) — exit codes, statuses and refusals in full
- [API.md](../../src/Cli/API.md) — the full CLI contract: every command, option and document shape
