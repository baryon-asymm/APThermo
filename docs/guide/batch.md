# Batch solving

## Purpose

Solve multiple rocket problems in a single batch call: either one propellant with a
list of problem definitions (different chamber pressures, area ratios, or both), or
an oxidizer-to-fuel sweep expressed as a list of mixtures and a list of problems, all
in one solver invocation.

## When to use

- You need to sweep over chamber pressure or area ratio for the same propellant.
- You need to sweep over the oxidizer-to-fuel ratio: build one mixture per ratio and
  solve them as one batch, rather than one solver call per ratio.
- You want to compare several cases without a separate solver call per case: a batch
  runs in as few launches as its case mix requires — cases are grouped by exit layout
  and, within a layout, by whether transport was requested, and each group is chunked
  into launches of at most `EngineOptions.ChunkSize` cases (16 384 by default) — which
  is the point of batching at all.

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build the propellant (oxidizer, fuel, O/F ratio).
3. Build a list of `RocketProblem` instances, one per case.
4. Create a solver and call `Solve` with the list; it returns one result per problem,
   in the order given. Check every result's stations' `Status` before reading a
   figure.

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
var results = solver.Solve(propellant, problems);

output.WriteLine("LOX/LH2 O/F=6.0  area ratios [20, 77.5]");
foreach (var result in results)
{
    var exit = result.Stations[^1];
    var chamberPressure = result.Problem.ChamberPressure / 1e6;
    if (exit.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  Pc={chamberPressure:F1} MPa  FAILED: {exit.Status}");
        continue;
    }

    output.WriteLine($"  Pc={chamberPressure:F1} MPa   Isp_exit={exit.Performance!.Value.SpecificImpulse:F1} m/s");
}
```

### An oxidizer-to-fuel sweep as one batch

`Solve` also takes a list of `ElementalMixture` alongside a list of `RocketProblem`,
one case per index: build one mixture per ratio with `Solver.MixtureOf`, then call
that overload. This is one batch, not one call per ratio:

<!-- snippet: RatioSweepUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
```

<!-- snippet: RatioSweep -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)   // K
    .Fuel("H2(L)", temperature: 20.27)        // K
    .OxidizerToFuelRatio(6.0)
    .Build();

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

double[] ratios = [5.0, 6.0, 7.0];
var mixtures = ratios.Select(ratio => solver.MixtureOf(propellant, ratio)).ToList();
var problems = ratios.Select(_ => new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] }).ToList();

var results = solver.Solve(mixtures, problems);

output.WriteLine("LOX/LH2 O/F sweep, one batch, Pc=7.0 MPa");
for (var i = 0; i < ratios.Length; i++)
{
    var exit = results[i].Stations[^1];
    if (exit.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  O/F={ratios[i]:F1}  FAILED: {exit.Status}");
        continue;
    }

    output.WriteLine($"  O/F={ratios[i]:F1}  Isp_exit={exit.Performance!.Value.SpecificImpulse:F1} m/s");
}
```

## Errors

- **A single case fails numerically**: the batch still returns all results; the
  failed case's stations carry a status other than `Ok` and a zero state, while the
  other cases are solved normally.
- **An empty batch**: `ArgumentException` at `Solve`, before any kernel runs.
- **A mixture of the sweep overload weighs off one kilogram**: `MixtureMassException`
  naming the mixture's index, before any kernel runs (see
  [Troubleshooting](troubleshooting.md)).

## See also

- [Rocket solving](rocket.md) — a single rocket problem, with exits and a custom propellant
- [Equilibrium states](equilibrium.md) — equilibrium without a nozzle
- [State records](states.md) — state records from another simulation, batched the same way
- [Command line](cli.md) — the `apthermo` tool's own `sweep` document field
