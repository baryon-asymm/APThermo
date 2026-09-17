# Getting started

## Purpose

Install APThermo and solve your first rocket case, in C# and from the command line.

## When to use

- This is your first time here and you want a working install before picking a
  specific workflow.
- You want to confirm a plain install runs without a GPU: the CPU accelerator needs
  nothing beyond the package and ILGPU.

## Steps

1. Install the library:

```
dotnet add package APThermo
```

2. Load the bundled NASA database, build a propellant, define a rocket problem, and
   solve. Nothing here reads a file: the database is embedded in the package.
   `output` is any `TextWriter` (`Console.Out` in a console application):

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

3. Install the command-line tool, if you also want to solve from a shell:

```
dotnet tool install --global APThermo.Cli
```

4. Solve the same propellant and chamber pressure from a JSON document instead of
   C#:

```console
$ apthermo rocket samples/cli/problems/rocket.json --accelerator cpu
```

The result document is written to standard output; see [Command line](cli.md) for
every option, and [Rocket solving](rocket.md) for the full C# station-by-station
walkthrough (exits, transport, flow model).

## Errors

- **A propellant build refuses** (unknown reactant, temperature out of range, a
  mixture rule violation): an exception at `Build`, before any solve is attempted.
  See [Rocket solving](rocket.md#errors).
- **A solve returns a status other than `Ok`** instead of throwing: always check
  `Status` before reading a figure. See [Troubleshooting](troubleshooting.md).
- **The CLI exits non-zero**: see [Command line](cli.md#errors) for the exit codes.

## See also

- [Rocket solving](rocket.md) — every station, exits and transport
- [Command line](cli.md) — every command, option and exit code
- [GPU acceleration](gpu.md) — using CUDA once the CPU path works
- [Troubleshooting](troubleshooting.md) — what to check when a run fails
