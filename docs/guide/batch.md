# Batch solving

## Purpose

Solve multiple rocket problems in a single batch call: given one propellant and a list of problem definitions (different chamber pressures, area ratios, or both), compute all results in one solver invocation.

## When to use

- You need to sweep over chamber pressure or area ratio for the same propellant.
- You want to compare several nozzle geometries without creating a separate solver per case.
- You are building a parameter study and want consistent solver state across cases.

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build the propellant (oxidizer, fuel, O/F ratio).
3. Build a list of `RocketProblem` instances, one per case.
4. Create a solver and call `Solve` with the list; it returns one result per problem.

<!-- snippet: BatchSolveUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
```

`output` is any `TextWriter` (`Console.Out` in a console application):

<!-- snippet: BatchSolve -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)   // K
    .Fuel("H2(L)", temperature: 20.27)        // K
    .OxidizerToFuelRatio(6.0)
    .Build();

double[] pressures = [5.0e6, 7.0e6, 10.0e6];   // Pa
var problems = pressures.Select(pressure => new RocketProblem
{
    ChamberPressure = pressure,
    AreaRatios = [20.0, 77.5],
    Flow = FlowModel.ShiftingEquilibrium,
}).ToList();

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
IReadOnlyList<RocketResult> results = solver.Solve(propellant, problems);

output.WriteLine("LOX/LH2 O/F=6.0  area ratios [20, 77.5]");
foreach (RocketResult result in results)
{
    Station exit = result.Stations[^1];
    var chamberPressure = result.Problem.ChamberPressure / 1e6;
    if (exit.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  Pc={chamberPressure:F1} MPa  FAILED: {exit.Status}");
        continue;
    }

    output.WriteLine($"  Pc={chamberPressure:F1} MPa   Isp_exit={exit.Performance!.Value.SpecificImpulse:F1} m/s");
}
```

## Errors

- **A single case fails numerically**: the batch still returns all results; the failed case carries a status and no station data, while the other cases are solved normally.
- **All cases share one propellant**: if you need different O/F ratios per case, build separate propellants and call `Solve` once per propellant, or use the command line's sweep form.

## See also

- [Rocket solving](rocket.md) — a single rocket problem
- [Equilibrium states](equilibrium.md) — equilibrium without a nozzle
- [States records](states.md) — state records from another simulation
- [Command line](cli.md) — the `apthermo` tool with sweep documents
