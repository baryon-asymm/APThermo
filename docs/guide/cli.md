# Command line

## Purpose

Use the `apthermo` command-line tool to solve rocket, equilibrium and states problems from JSON documents, list database species, inspect available accelerators, and print the JSON Schemas of the document shapes.

## When to use

- Scripting: you want to drive APThermo from a shell script or CI pipeline without writing C#.
- Batch processing: you have many problem documents and want to run them in a loop.
- Inspection: you want to see the database species, the available accelerators, or the schema of a document shape.

## Steps

1. Install the tool globally: `dotnet tool install --global APThermo.Cli`
2. Write your problem as a JSON document (see [Rocket solving](rocket.md) for the shape; run `apthermo schema input` to print the full schema).
3. Run the appropriate command, pinning `--accelerator cpu` for reproducible output:

```console
$ apthermo rocket samples/cli/problems/rocket.json --accelerator cpu
```

4. The JSON result document is written to standard output. Redirect it to a file or pipe it to another tool.

Other commands:

- `apthermo equilibrium problem.json` — one tp, hp or sp state
- `apthermo states records.json` — a batch of state records
- `apthermo species --find H2O` — search the database
- `apthermo devices` — list available accelerators
- `apthermo schema input|output|states|species|devices` — print a JSON Schema

## Errors

Exit codes:

| Code | Meaning |
|---|---|
| 0 | All cases solved successfully. |
| 1 | At least one case or station failed numerically; the document is still written with per-case status. |
| 2 | Invalid input document, unknown option, bad database path, unknown reactant, or schema name. A message on standard error names the problem. |
| 3 | Accelerator unavailable (no CUDA driver, ILGPU mismatch) or an unexpected infrastructure failure. |

Common exit-code-2 situations: a JSON field with the wrong type, a species name not in the database, a composition that does not weigh one kilogram, a `--database` path that does not exist.

## See also

- [Rocket solving](rocket.md) — the C# API for rocket problems
- [Batch solving](batch.md) — multiple problems in one call
- [Equilibrium states](equilibrium.md) — single equilibrium states
- [States records](states.md) — state records from another simulation
- [API.md](../../src/Cli/API.md) — the full CLI contract: every command, option and document shape
