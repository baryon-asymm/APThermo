# APThermo

Chemical equilibrium and performance of rocket propellant combustion products: Gibbs-energy minimization (Gordon–McBride), shifting-equilibrium and frozen flow, CPU and CUDA.

## Quick start

Install the library:

```
dotnet add package APThermo
```

Load the bundled NASA database, build a propellant, define a rocket problem, and solve
(`output` is any `TextWriter`, `Console.Out` in a console application):

<!-- snippet: QuickStartUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
```

<!-- snippet: QuickStart -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)   // K
    .Fuel("H2(L)", temperature: 20.27)        // K
    .OxidizerToFuelRatio(6.0)
    .Build();

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
RocketResult result = solver.Solve(propellant, new RocketProblem
{
    ChamberPressure = 7.0e6,   // Pa
    AreaRatios = [20.0],
});

Station chamber = result.Stations[0];
if (chamber.Status == CaseStatus.Ok)
{
    output.WriteLine($"chamber temperature = {chamber.State.Temperature:F1} K");
}
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
