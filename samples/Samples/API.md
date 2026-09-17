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
    public static int Run(string[] args, TextWriter output, TextWriter error);   // args = [scenario name]; 0 a solved scenario, 2 an unknown name or none (the usage on error)
    internal static string ClassNameOf(string scenario);          // the scenario's source class name, for L2's approved-output lookup
}
```

The only reader is the docs tests node (`APThermo.Docs.Tests`), which holds the grant.

⚠ 2026-09-17: added `ClassNameOf` and widened the entry point's own reading to include
it. Before, `tests/Docs.Tests/GuideDocuments.cs` carried a second, hand-written
`scenario → class name` switch (finding S11, fixed in `900e5a4`); `ClassNameOf`
reflects on the scenario table's own delegates, so the mapping has one source.

## Scenarios ✅

One `internal sealed` class per scenario with `internal static void Run(TextWriter
output)`. Two snippet regions per scenario, `// snippet-start: <name>` /
`// snippet-end`: `<ClassName>Usings` (the `using` lines) and `<ClassName>` (the body of
`Run`), quoted verbatim by the guide after the common indentation is stripped. The CPU
accelerator is pinned (`AcceleratorKind.Cpu`) everywhere but `AcceleratorChoice`, so the
output is reproducible on every machine.

| Scenario name | Class | Answers | Prints | Approved output |
|---|---|---|---|---|
| `quick-start` | `QuickStart` | the smallest solve | the chamber temperature of one rocket case | `QuickStart.approved.txt` |
| `rocket` | `RocketSolve` | a full rocket case with transport | every station's status, T, P, Isp; the throat's viscosity and top mole fractions | `RocketSolve.approved.txt` |
| `rocket-exits` | `RocketExits` | mixed pressure-ratio and area-ratio exits, frozen at the throat | every station's status, T and Isp | `RocketExits.approved.txt` |
| `custom-propellant` | `CustomPropellant` | a custom reactant, mass fractions, an omitted species | every station's status, T and Isp | `CustomPropellant.approved.txt` |
| `equilibrium-kinds` | `EquilibriumKinds` | the three problem kinds via `Kind` | hp, sp (at the hp state's entropy) and tp, each with status and T | `EquilibriumKinds.approved.txt` |
| `batch` | `BatchSolve` | a chamber-pressure sweep in one batch | one line per pressure with the exit status and Isp | `BatchSolve.approved.txt` |
| `ratio-sweep` | `RatioSweep` | an O/F sweep in one batch, over mixtures | one line per ratio with the exit status and Isp | `RatioSweep.approved.txt` |
| `states` | `StatesSolve` | literal element moles as state records | the mixture's mass, then one line per record with status, T and h | `StatesSolve.approved.txt` |
| `rocket-states` | `RocketStates` | a state record with exits | `SolveRocketStates`'s mass and every station's status, T and Isp | `RocketStates.approved.txt` |
| `accelerator-choice` | `AcceleratorChoice` | binding through `AcceleratorProbe` and `Auto` | machine-independent facts only: that `Describe` answered, and that `Auto` agrees with the CPU accelerator | `AcceleratorChoice.approved.txt` |
| `database` | `DatabaseFromFiles` | loading from the committed `data/` files | the product and reactant counts, the start of the thermo hash | `DatabaseFromFiles.approved.txt` |
| `failures` | `Failures` | the statuses and refusals a consumer must be ready for | an unknown reactant, a named reactant with a ratio, a state record with two targets, a mixture beyond its mass tolerance, and one failed station | `Failures.approved.txt` |

Every approved file lives under `tests/Docs.Tests/approved/samples/`.

⚠ 2026-09-17: before the audit (its finding S2, fixed in `900e5a4`), this table named
four scenarios — `RocketSolve`, `BatchSolve`, `EquilibriumSolve`, `StatesSolve` — one
per solve form of the root `API.md` entry point, and none checked a `Status` before
reading a figure (S1). The set above answers the questions the audit's scenario table
(section 2) lists that the four did not: exits by pressure ratio, a custom reactant, the
three equilibrium kinds, an O/F sweep as one batch, state records with exits, the
accelerator choice, and the refusals. `EquilibriumSolve` is renamed `EquilibriumKinds`
(it now shows all three kinds, not hp alone); `RocketSolve`, `BatchSolve` and
`StatesSolve` keep their names, with their content corrected (transport now printed,
the chamber's meaningless `Isp=0` no longer printed, `StatesSolve`'s composition now
literal instead of derived from a propellant). Every scenario is quoted at least once:
`QuickStart` by `README.md` and `docs/nuget/APThermo.md`, and the others by the guide
pages under `docs/guide/`.

⚠ 2026-09-17: this paragraph said that `RocketExits`, `CustomPropellant`,
`RatioSweep`, `RocketStates`, `AcceleratorChoice`, `DatabaseFromFiles` and `Failures`
were "not yet reachable from a guide page". That was true when the samples were
rewritten (`39faf5b`). The guide rewrite merged as `e229d2e` quotes all twelve.

## Errors ✅

| Situation | Behaviour |
|---|---|
| no argument, or a name that is not a scenario | the usage naming the scenarios on standard error, exit code 2 |
| a scenario's own solve is refused (an unknown reactant, a mixture beyond its tolerance, a state record with two targets) | caught and printed by `Failures` alone; every other scenario solves a case chosen to succeed |

## Side effects ✅

Loads the database embedded in `APThermo` (or, for `DatabaseFromFiles`, the committed
`data/thermo.inp` and `data/trans.inp`), creates the CPU accelerator the solver asks
for (`AcceleratorChoice` also creates one bound to `Auto`), and prints to the writer it
is given. No network, no environment variable.

⚠ 2026-09-17: `## Errors` and `## Side effects` were marked ⏳ although `Program.cs`
already implemented both (finding S10, fixed in `900e5a4`); both are ✅ now, and
their content is re-checked against the code above.
