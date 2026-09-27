# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.2.0] - 2026-09-27

This release breaks the binary surface of 0.1.0: code built against 0.1.0 must be
recompiled. It fixes the findings of a hidden-defect audit of the whole library, the
most important being that 0.1.0 could not run on CUDA on any GPU older than Blackwell.

### Changed
- `MixtureState`, `PerformanceFigures` and `TransportFigures` expose auto-properties
  instead of public fields and implement `IEquatable<T>` with `==` and `!=`. Source
  that reads or sets them compiles unchanged; binaries built against 0.1.0 must be
  recompiled.
- Inputs that 0.1.0 accepted silently, or answered wrongly, are now refused by name:
  - a rocket exit at an area ratio of exactly 1 is `AreaRatioInvalid`; the area ratio
    must be greater than 1, as in NASA CEA;
  - a `FlowModel` value other than the three named ones is `InvalidInput`; 0.1.0
    solved it as frozen at the throat;
  - a propellant group (oxidizer, fuel or named reactants) that mixes mass-fraction
    and mole amounts is refused, instead of summing the two as one unit;
  - an element with no candidate species, or with no monatomic record to take its
    atomic weight from, is refused, instead of being silently dropped or ending in an
    unrelated numerical status;
  - `EngineOptions.LibNvvmPath` without `LibDevicePath`, or the reverse, is an
    `ArgumentException` naming the missing option, instead of falling back to
    discovery.
- Command line:
  - the CSV output's input columns carry the prefix `inputs.`, so an input never
    repeats a result field's name (`pressure`, `temperature`, `enthalpy` and
    `entropy` used to appear twice, and a rocket record's exit row read back the exit
    pressure under the chamber's column);
  - a JSON document with a member given twice is refused with exit 2, naming the
    field, instead of keeping the last value;
  - an option given an empty value (`--output=`, `--database=`) is exit 2, instead of
    an unhandled exception or reading `thermo.inp` from the working directory;
  - `--mass-tolerance` is refused (exit 2) on a document whose propellant is given by
    reactants, since it applies only to element moles given directly;
  - a refused state record is named by its own file and line, not by its position in
    an internal batch.
- The CPU accelerator runs as many threads as the machine has logical cores, rounded
  down to a multiple of 4. 0.1.0 always ran 16. Results do not depend on the count.
- The build enables every compiler and analyzer diagnostic and treats each one as an
  error, with no suppression anywhere.

### Added
- The standard exception constructors (parameterless, message, message and inner
  exception) on `DatabaseFormatException`, `AcceleratorUnavailableException`,
  `StateRecordException` and `MixtureMassException`.

### Fixed
- GPU (CUDA):
  - 0.1.0 threw on every CUDA run on GPUs of compute capability 7.5 to 9.0 (Turing to
    Hopper). ILGPU 1.5.3 completes the libdevice functions itself on those
    architectures, and the library then added them a second time. Every architecture
    from 7.5 up is now compiled for and run on the reference device (a Blackwell GPU);
    an engine reports CUDA as bound only after a kernel has loaded on it.
  - A CUDA library that exists but cannot load no longer reaches the device: 0.1.0
    left a raw exception and about 190 MiB of device memory per attempt.
  - Under WSL, the second CUDA engine of a process failed to bind (an ILGPU 1.5.3
    defect there); every engine now binds.
  - A NaN in the minimum or maximum of the numerical code propagated on the CPU and
    was dropped on CUDA, so a failing case could report `Ok`, or a station velocity
    of 0 instead of NaN, on the GPU only. Both accelerators now behave as .NET does.
  - Very large `ScratchBytes` and `ChunkSize` settings could overflow a kernel's
    32-bit offsets; a chunk is now capped below that.
- Equilibrium and condensed phases:
  - A condensed species' temperature range now spans all its intervals, as NASA CEA
    reads it; nine condensed records between 298.15 K and 300 K were excluded before.
  - Ice (`H2O(cr)`) may appear below 200 K, the lower end of the gas data.
  - A state with more than eight stable condensed phases dropped the excess and
    reported `Ok`; up to twenty are now held, and an `Ok` result is checked to leave
    no condensed species that should have entered.
  - A table whose elements include Na, C and N (for example NaNO3 with RP-1) was
    refused, because `NaCN(II)` has six temperature intervals and the limit was five.
  - Two records of sodium cyanide were split at their own fit noise into phantom
    phase transitions.
  - A solve started from a previous solution could end `NotConverged` or
    `SingularMatrix` where a cold start converges: when a gas crossed the trace
    threshold on the last step, or when a condensed species of the previous state
    cannot exist at the new one. Both now converge.
  - The frozen composition's mole numbers are validated before `Ok` is reported.
- Rocket performance:
  - At the high-pressure edge of a melting plateau (for example Al2O3 in an
    aluminized propellant), where the sound speed jumps, the throat search ended
    `ThroatNotFound`. The throat is now the point of largest mass flux, as RP-1311
    defines it, and there its Mach number is below 1. NASA CEA reports a wrong c*
    there, about 6 % high in the worst measured case.
  - An exit found by area ratio is accepted only on the supersonic branch.
  - The throat's pressure ratio, and the start of the exit stations, come from the
    state actually solved.
  - A station whose entropy departs from the chamber's is `NotConverged`.
- Transport:
  - Where two species of the transport set were chemically proportional, the reacting
    conductivity and heat capacity were computed from reactions that did not conserve
    the elements, under status `Ok`; the reduction now follows NASA CEA.
  - A species with viscosity data but no conductivity data (`UF6`) is now counted in
    `EstimatedSpeciesCount` and `EstimatedMoleFraction`.
- Front door and data:
  - A reactant name with several database records takes the union of their
    temperature ranges, and a reactant-only name with several records resolves to the
    last one, as NASA CEA does (`n-Butanol` resolved to the wrong record).
  - A formula pair with a symbol and a zero count is dropped when the database is
    read.
  - A malformed database line is reported with one `file:line:` prefix, not two.

## [0.1.0] - 2026-09-18

### Added
- Initial public release of Aerospace Propellant Thermodynamics (APThermo): rocket
  propellant chemical equilibrium composition, thermodynamic and transport
  properties, and rocket engine performance (NASA CEA's class of computation), one
  numerical program in double precision that runs on the CPU or, for batches, on an
  NVIDIA GPU (CUDA).
- Two packages: `APThermo`, the library — seven assemblies (`Data`, `Thermo`,
  `Equilibrium`, `Performance`, `Transport`, `Execution`, `Problems`) merged into one
  package, with ILGPU as its only dependency; and `APThermo.Cli`, the `apthermo` .NET
  tool, packing the same assemblies and ILGPU as plain files, with no NuGet
  dependency of its own.
- Supported platforms: Windows x64 and Linux x64, both on the CPU accelerator and on
  CUDA (an NVIDIA driver with CUDA 12.8 or newer, plus libnvvm and
  `libdevice.10.bc` from a CUDA Toolkit 12.8 or newer).
- SI units throughout the public surface (K, Pa, J/kg, J/(kg·K), kg/kmol, kg/m³, m/s,
  Pa·s, W/(m·K)); specific impulse is the effective exhaust velocity in m/s, with a
  seconds conversion available from the command line only.
- `apthermo` CLI: `rocket`, `equilibrium`, `states`, `species`, `devices` and `schema`
  commands, JSON in, JSON or CSV out, with an embedded NASA database used by default
  and a `--database` override.
- Twelve consumer sample scenarios under `samples/Samples`: a quick start, a full
  rocket case with transport, mixed pressure-ratio and area-ratio exits, a custom
  reactant, the three equilibrium problem kinds, a chamber-pressure batch sweep, an
  O/F ratio sweep, literal element-mole state records with and without exits,
  accelerator choice, loading the database from files, and the refusals a consumer
  must be ready for.
- Task-oriented guide under `docs/guide/` (getting started, rocket, equilibrium,
  batch, states, data, GPU, the command line, troubleshooting), with every C# block
  and every `apthermo` invocation checked against the samples and their approved
  output.
- JSON Schemas embedded in the CLI (`apthermo schema <name>`).

[Unreleased]: https://github.com/baryon-asymm/APThermo/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/baryon-asymm/APThermo/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/baryon-asymm/APThermo/releases/tag/v0.1.0
