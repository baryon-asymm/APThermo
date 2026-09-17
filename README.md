# APThermo

Chemical equilibrium and performance of rocket propellant combustion products: Gibbs-energy minimization (Gordon–McBride), shifting-equilibrium and frozen flow, CPU and CUDA.

## Quick start

Install the library:

```
dotnet add package APThermo
```

Load the bundled NASA database, build a propellant, define a rocket problem, and solve:

```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;

var database = SpeciesDatabase.LoadBundled();

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)
    .Fuel("H2(L)", temperature: 20.27)
    .OxidizerToFuelRatio(6.0)
    .Build();

var problem = new RocketProblem
{
    ChamberPressure = 7.0e6,
    AreaRatios = [20.0, 77.5],
    Flow = FlowModel.ShiftingEquilibrium,
    Transport = true,
};

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
RocketResult result = solver.Solve(propellant, problem);
```

See [Rocket solving](docs/guide/rocket.md) for the full walkthrough.

## Command line

Install the tool:

```
dotnet tool install --global APThermo.Cli
```

Run a rocket problem from a JSON document:

```console
$ apthermo rocket samples/cli/problems/rocket.json --accelerator cpu
```

See [Command line](docs/guide/cli.md) for every command, option and exit code.

## Guide

- [Rocket solving](docs/guide/rocket.md)
- [Batch solving](docs/guide/batch.md)
- [Equilibrium states](docs/guide/equilibrium.md)
- [States records](docs/guide/states.md)
- [Command line](docs/guide/cli.md)

## Agent entry point

See [llms.txt](llms.txt).
