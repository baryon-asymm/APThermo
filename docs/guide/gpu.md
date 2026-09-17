# GPU acceleration

## Purpose

Choose and verify the accelerator a solve runs on — the CPU accelerator, which needs
nothing beyond the package, or CUDA, which needs an NVIDIA driver and the CUDA
libraries at run time — and diagnose why an `Auto` run fell back to the CPU.

## When to use

- You want the CUDA path for a large batch (rockets, sweeps, state records): CUDA is
  faster only for large batches, not for a single case.
- You are deploying to a machine or a container without a GPU and want to confirm
  the CPU path runs without CUDA installed at all.
- A run you expected to use CUDA used the CPU instead, and you need to know why.

## Steps

1. **Requirements.** The CPU accelerator (`AcceleratorKind.Cpu`) needs nothing beyond
   the package and ILGPU. CUDA (`AcceleratorKind.Cuda`, or `Auto` when a usable GPU
   is found) additionally needs, at run time:
   - an NVIDIA driver with CUDA 12.8 or newer;
   - `libnvvm` (`nvvm64_40_0.dll` on Windows, under the toolkit's `nvvm/bin/x64`;
     `libnvvm.so` on Linux, including under WSL2, under `nvvm/lib64`) and
     `libdevice.10.bc`, both from an NVIDIA CUDA Toolkit 12.8 or newer.
   Windows x64 and Linux x64 are both supported, on the CPU accelerator and on CUDA.
2. **Discovery order**, read by `Engine.Create` (reached through `Solver.Create` or
   `AcceleratorProbe.Describe`): `EngineOptions.LibNvvmPath`/`LibDevicePath` when
   given, then, when `LibDeviceDiscovery` is true (the default), the environment
   variables `CUDA_PATH` and `ProgramFiles` on Windows, `CUDA_HOME` on Linux, and the
   toolkit's own directory layout under them.
3. **Choosing an accelerator kind**: `AcceleratorKind.Auto` (the default) binds CUDA
   when it is not forbidden and every library and device is found, and falls back to
   the CPU accelerator otherwise — including when the CUDA context itself cannot be
   created — without throwing. `Cpu` always binds the CPU accelerator. `Cuda` binds
   CUDA or throws `AcceleratorUnavailableException`, naming the missing piece and
   every path tried.
4. **Forbidding CUDA outright**: the environment variable `APTHERMO_NO_CUDA=1`
   (`EngineOptions.NoCudaVariable`) makes every accelerator kind but `Cpu` refuse or
   fall back, everywhere in the tree. This is how the fast test suite and CI prove
   the CPU path needs no CUDA software at all.
5. **Checking what you got**, without running a case: `AcceleratorProbe.Describe`
   binds as `Solver.Create` would and releases the device, returning an
   `AcceleratorInfo` with `Kind`, `DeviceName`, `IlgpuVersion` and, when `Auto` fell
   back, `CudaSkippedBecause` naming the reason. Call it once per accelerator kind,
   not per case — creating a CUDA context takes time.

<!-- snippet: AcceleratorChoiceUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
```

`output` is any `TextWriter` (`Console.Out` in a console application):

<!-- snippet: AcceleratorChoice -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var info = AcceleratorProbe.Describe(new EngineOptions { Accelerator = AcceleratorKind.Auto });
var versionKnown = !string.IsNullOrEmpty(info.IlgpuVersion);
output.WriteLine($"AcceleratorProbe.Describe answered without throwing, IlgpuVersion is not empty: {versionKnown}");

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)   // K
    .Fuel("H2(L)", temperature: 20.27)        // K
    .OxidizerToFuelRatio(6.0)
    .Build();
var problem = new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] };   // Pa

using var cpuSolver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
using var autoSolver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Auto });

var cpuTemperature = cpuSolver.Solve(propellant, problem).Stations[1].State.Temperature;
var autoTemperature = autoSolver.Solve(propellant, problem).Stations[1].State.Temperature;
var agrees = Math.Abs(cpuTemperature - autoTemperature) <= 1.0e-4 * cpuTemperature;

output.WriteLine($"the Auto-chosen accelerator agrees with the CPU accelerator on the throat temperature: {agrees}");
```

6. **From the command line**: `apthermo devices` lists the CPU and, when found, the
   CUDA accelerator, with `cudaSkippedBecause`; `--accelerator auto|cpu|cuda`
   overrides a document's `engine.accelerator`; every output document's
   `run.accelerator` carries the same fields as `AcceleratorInfo`, so a result
   document is self-describing about what it ran on.

```console
$ apthermo devices
```

## Errors

| Situation | Behaviour |
|---|---|
| `Cuda` requested and CUDA is forbidden, no libnvvm or libdevice found, no device at the index, or the context cannot be created | `AcceleratorUnavailableException`, naming the missing piece and every path tried |
| `Auto` requested and the same failures occur | silent fallback to the CPU accelerator; `AcceleratorInfo.CudaSkippedBecause` names the reason |
| the installed ILGPU version, or a reflected member it exposes, does not match what the tree expects | `InvalidOperationException` naming the ILGPU version, at `Solver.Create` or `AcceleratorProbe.Describe` |
| a kernel's compiled program calls a libdevice wrapper this tree has no fragment for, or libnvvm/the driver refuses the linked module | `InvalidOperationException` naming the wrapper or carrying the compiler's log, on that program's first run |

## See also

- [Command line](cli.md) — `--accelerator`, `apthermo devices`
- [The database](data.md) — what a run reads regardless of accelerator
- [Troubleshooting](troubleshooting.md) — every exit code and status in one place
- [API.md](../../src/Execution/API.md) — `EngineOptions`, `AcceleratorInfo`, `AcceleratorProbe`
