# APThermo.Cli

`apthermo` is the command-line front end of [APThermo](https://www.nuget.org/packages/APThermo):
a JSON-in, JSON-or-CSV-out tool for rocket propellant chemical equilibrium and
performance (chamber, throat, nozzle exit; shifting-equilibrium and frozen flow), the
class of tool NASA CEA belongs to.

## Install

```
dotnet tool install --global APThermo.Cli
```

## Minimal example

A rocket problem document, LOX/LH2 at an oxidizer-to-fuel ratio of 6
(`samples/cli/problems/rocket.json` in the repository):

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

Run it with `apthermo rocket samples/cli/problems/rocket.json --accelerator cpu`: it
writes the chamber, throat and exit states and the performance figures (specific
impulse, thrust coefficient, characteristic velocity) as JSON to standard output. No
`--database` is needed: `apthermo` uses the NASA thermodynamic database embedded in
`APThermo`. Run `apthermo --help` for every command, and `apthermo --version` for
the tool's version.

## CUDA

`--accelerator cpu` needs nothing beyond the tool. `--accelerator cuda` (or `auto` on
a machine with a usable GPU) additionally needs, at run time:

- an NVIDIA driver with CUDA 12.8 or newer;
- `libnvvm` (`nvvm64_40_0.dll` on Windows, `libnvvm.so` on Linux) and
  `libdevice.10.bc`, both from an NVIDIA CUDA Toolkit 12.8 or newer.

`auto` falls back to the CPU accelerator when no usable CUDA device or library is
found; the output document's `run.accelerator.cudaSkippedBecause` names the reason.

## Guide

Every command, its documents and its exit codes: see `docs/guide/cli.md` in the
source repository.

## License and data notice

This package is MIT-licensed. It embeds the NASA CEA thermodynamic and transport
databases (through `APThermo`), licensed Apache-2.0; the package includes the
upstream `NOTICE`. See the package's license expression (`MIT AND Apache-2.0`) and
the embedded `NOTICE` file for the full attribution.
