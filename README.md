# APThermo

Chemical equilibrium and performance of rocket propellant combustion products:
Gibbs-energy minimization (Gordon–McBride), shifting-equilibrium and frozen flow, on
the CPU and, for large batches, on an NVIDIA GPU (CUDA) in double precision. It is the
class of tool NASA CEA belongs to.

## Install

```
dotnet add package APThermo
```

```
dotnet tool install --global APThermo.Cli
```

The first is the library; the second is `apthermo`, its command-line front end. The
NASA thermodynamic database is embedded in both, so neither needs data files on disk.

## Quick start

Load the bundled database, build a propellant, define a rocket problem, and solve
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

The same case from the command line, over a JSON document. Save this as
`samples/cli/problems/rocket.json` (already there in a clone of the repository):

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

See [Getting started](docs/guide/getting-started.md) for the full walkthrough, both
ways.

## Platforms and CUDA

Windows x64 and Linux x64 are both supported on the CPU accelerator, which needs
nothing beyond the package. CUDA is supported on both too, but Linux verification is
still pending as of this release (the root `BOOT.md`'s Linux acceptance criterion,
the fast suite on the CPU accelerator and the execution tests node's CUDA sweep under
WSL2 on the reference machine, is unticked). CUDA additionally needs, at run time, an
NVIDIA driver with CUDA 12.8 or newer, plus `libnvvm` and `libdevice.10.bc` from a
CUDA Toolkit 12.8 or newer.
`AcceleratorKind.Auto` falls back to the CPU accelerator when no usable GPU or library
is found. See [GPU acceleration](docs/guide/gpu.md) for the discovery order and how to
check which accelerator a run used.

## Units

SI throughout: K, Pa, J/kg, J/(kg·K), kg/kmol, kg/m³, m/s, Pa·s, W/(m·K). Specific
impulse is the effective exhaust velocity in m/s; the conversion to seconds
(dividing by g0 = 9.80665 m/s²) happens only in the command-line front end.

## Guide

- [Getting started](docs/guide/getting-started.md)
- [Rocket solving](docs/guide/rocket.md)
- [Batch solving](docs/guide/batch.md)
- [Equilibrium states](docs/guide/equilibrium.md)
- [State records](docs/guide/states.md)
- [GPU acceleration](docs/guide/gpu.md)
- [The database](docs/guide/data.md)
- [Command line](docs/guide/cli.md)
- [Troubleshooting](docs/guide/troubleshooting.md)

## Agent entry point

See [llms.txt](llms.txt).

## License and data notice

This repository is MIT-licensed. It embeds the NASA CEA thermodynamic and transport
databases, licensed Apache-2.0; see [`data/NOTICE`](data/NOTICE) for the full
attribution, or call `SpeciesDatabase.BundledNotice()` at run time.
