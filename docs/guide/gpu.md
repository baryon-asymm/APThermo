# GPU acceleration

## Purpose

Choose and verify the accelerator a solve runs on — the CPU accelerator, which needs
nothing beyond the package, or CUDA, which needs an NVIDIA driver and the CUDA
libraries at run time — and diagnose why an `Auto` run fell back to the CPU.

## When to use

- You want the CUDA path for a batch (rockets, sweeps, state records): CUDA already
  wins at 1 000 cases (2.3× on the reference machine — 16.07 ms against 36.25 ms for a
  1 000-case rocket batch, the After-1 column of
  `tests/Benchmarks/results/comparison-2026-09-15.md`, Group 1) and further ahead at
  larger batch sizes; a single case does not amortize the CUDA context creation and
  kernel compile, so the CPU accelerator stays the better choice there.
- You are deploying to a machine or a container without a GPU and want to confirm
  the CPU path runs without CUDA installed at all.
- A run you expected to use CUDA used the CPU instead, and you need to know why.

## Steps

1. **Requirements.** The CPU accelerator (`AcceleratorKind.Cpu`) needs nothing beyond
   the package and ILGPU. CUDA (`AcceleratorKind.Cuda`, or `Auto` when a usable GPU
   is found) additionally needs, at run time:
   - an NVIDIA driver with CUDA 12.8 or newer;
   - `libnvvm` (`nvvm64_40_0.dll` on Windows, tried first under the toolkit's
     `nvvm/bin`, then under `nvvm/bin/x64` — a 13.x toolkit keeps it at the latter;
     `libnvvm.so` on Linux, including under WSL2, under `nvvm/lib64`) and
     `libdevice.10.bc` (`nvvm/libdevice/libdevice.10.bc` on both platforms), both from
     an NVIDIA CUDA Toolkit 12.8 or newer.
   Windows x64 and Linux x64 are both supported on the CPU accelerator. CUDA is
   supported on both too, but Linux verification is still pending: the root
   `BOOT.md`'s Linux acceptance criterion (the fast suite on the CPU accelerator and
   the execution tests node's CUDA sweep, under WSL2 on the reference machine) is
   unticked as of this release.
2. **Discovery order**, applied when `Solver.Create` or `AcceleratorProbe.Describe`
   binds CUDA: `EngineOptions.LibNvvmPath`/`LibDevicePath` when both are given and
   exist, tried first. Otherwise, when `LibDeviceDiscovery` is true (the default): the
   `CUDA_PATH` environment variable, on either platform; then, Linux only, `CUDA_HOME`,
   the fixed directory `/usr/local/cuda`, and the versioned `/usr/local/cuda-*`
   directories (newest version first); Windows only, the versioned `v*` directories
   under `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA` (newest version first,
   `ProgramFiles` read from the environment). Under each candidate root in turn,
   `libnvvm` (the NVVM compiler library the CUDA Toolkit ships, not the driver) is
   tried at the paths of point 1 above and `libdevice.10.bc` at `nvvm/libdevice/`;
   discovery stops at the first root where both files are found, and
   `AcceleratorUnavailableException` names every path tried, in this order.
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
   not per case — creating a CUDA context takes time. For the accelerator a bound
   solver's own solves actually use, read `Solver.Accelerator` (`Problems`' API) once
   the solver is created: it is the same `AcceleratorInfo`, kept for the solver's
   lifetime rather than probed again.

<!-- snippet: AcceleratorChoiceUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
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

var cpuThroat = cpuSolver.Solve(propellant, problem).Stations[1];
var autoThroat = autoSolver.Solve(propellant, problem).Stations[1];

if (cpuThroat.Status == CaseStatus.Ok && autoThroat.Status == CaseStatus.Ok)
{
    var agrees = Math.Abs(cpuThroat.State.Temperature - autoThroat.State.Temperature) <= 1.0e-4 * cpuThroat.State.Temperature;
    output.WriteLine($"the Auto-chosen accelerator agrees with the CPU accelerator on the throat temperature: {agrees}");
}
else
{
    output.WriteLine($"the Auto-chosen accelerator agrees with the CPU accelerator on the throat temperature: skipped, throat status {cpuThroat.Status}/{autoThroat.Status}");
}
```

6. **From the command line**: `apthermo devices` reports what this machine offers —
   the CPU accelerator and, when CUDA was tried, either an accelerator description
   like the CPU's or, when CUDA is unavailable, `available: false` with a message and
   every path tried. `apthermo schema devices` prints the document's exact shape. The
   exact values are machine-dependent, so no sample output is shown here; run it on
   your own machine to see its devices. `--accelerator auto|cpu|cuda` overrides a
   document's `engine.accelerator`.

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
