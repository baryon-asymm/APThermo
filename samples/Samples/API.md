# API.md — Samples

Namespace `APThermo.Samples`. The consumer scenarios as running programs over the
package surface; the source of the guide's C# blocks. Not packed, part of no package.
Everything not listed here is internal and may change.

## Entry point (tree contract) ✅

```csharp
namespace APThermo.Samples;

internal static class Program
{
    public static IReadOnlyList<string> Scenarios { get; }        // the scenario names, in order
    public static int Main(string[] args);
    public static int Run(string[] args, TextWriter output, TextWriter error);   // args = [scenario name]; 0 a solved scenario, 2 an unknown name (the usage on error)
}
```

The only reader is the docs tests node (`APThermo.Docs.Tests`), which holds the grant.

## Scenarios ✅

One `internal sealed` class per scenario with `internal static void Run(TextWriter
output)`; the snippet region, delimited by a single `// <!-- snippet: <name> -->` marker
and running from that line to the end of the file, is what the guide quotes. The CPU
accelerator is pinned (`AcceleratorKind.Cpu`) so the output is reproducible on every
machine.

| Scenario | The solve form (root `API.md`, Entry points) | Prints | Approved output |
|---|---|---|---|
| `RocketSolve` | `Solver.Solve(propellant, problem)` → one `RocketResult` | the LOX/LH2 example: one line per station with its state and performance figures | `tests/Docs.Tests/approved/samples/RocketSolve.approved.txt` |
| `BatchSolve` | `Solver.Solve(propellant, problems)` → a batch of `RocketResult` | the ratio sweep: one line per case with the inputs that vary and the performance figures | `tests/Docs.Tests/approved/samples/BatchSolve.approved.txt` |
| `EquilibriumSolve` | `Solver.Solve(propellant, new EquilibriumProblem { … })` → one `EquilibriumResult` | the hp state at the propellant's enthalpy: the state's fields | `tests/Docs.Tests/approved/samples/EquilibriumSolve.approved.txt` |
| `StatesSolve` | `Solver.SolveStates(records)` → a batch of `EquilibriumResult` | the records of another simulation: one line per record with its state's fields | `tests/Docs.Tests/approved/samples/StatesSolve.approved.txt` |

## Errors ⏳

| Situation | Behaviour |
|---|---|
| no argument, or a name that is not a scenario | the usage naming the scenarios on standard error, exit code 2 |

## Side effects ⏳

Loads the database embedded in `APThermo`, creates the CPU accelerator the solver asks
for, and prints to the writer it is given. No file, no network.
