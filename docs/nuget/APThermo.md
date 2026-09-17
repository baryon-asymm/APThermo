# APThermo

APThermo computes the chemical equilibrium composition and the thermodynamic and
transport properties of rocket propellant combustion products, and from them the
performance of a rocket engine (chamber, throat, nozzle exit; shifting-equilibrium
and frozen flow). It is the class of tool NASA CEA belongs to: Gibbs-energy
minimization (Gordon–McBride), one numerical program that runs on the CPU and, for
large batches of states, on an NVIDIA GPU (CUDA) in double precision.

This package is the library. The command-line tool is `APThermo.Cli` (`apthermo`).

## Install

```
dotnet add package APThermo
```

## Minimal example

The NASA thermodynamic database (`thermo.inp`, `trans.inp`, from
[github.com/nasa/cea](https://github.com/nasa/cea), Apache-2.0) is embedded in this
package, so a solve needs no data files on disk. `output` is any `TextWriter`
(`Console.Out` in a console application).

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

## CUDA

The CPU accelerator (`AcceleratorKind.Cpu`) needs nothing beyond this package and
ILGPU. The CUDA accelerator (`AcceleratorKind.Cuda`, or `Auto` on a machine with a
usable GPU) additionally needs, at run time:

- an NVIDIA driver with CUDA 12.8 or newer;
- `libnvvm` (`nvvm64_40_0.dll` on Windows, `libnvvm.so` on Linux) and
  `libdevice.10.bc`, both from an NVIDIA CUDA Toolkit 12.8 or newer.

`AcceleratorKind.Auto` falls back to the CPU accelerator when no usable CUDA device
or library is found; a bound solver's `solver.Accelerator.CudaSkippedBecause` names
the reason (`AcceleratorProbe.Describe` answers the same question without solving a
case).

## Guide

The full guide has every scenario above and more (exits, a custom propellant,
batches, state records, GPU acceleration, the database, troubleshooting): see
[Getting started](https://github.com/OWNER/REPO/blob/main/docs/guide/getting-started.md).

## License and data notice

This package is MIT-licensed. It embeds the NASA CEA thermodynamic and transport
databases, licensed Apache-2.0; the package includes the upstream `NOTICE`. See the
package's license expression (`MIT AND Apache-2.0`) and the embedded `NOTICE` file
for the full attribution, or call `SpeciesDatabase.BundledNotice()` at run time.
