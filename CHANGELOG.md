# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

To be released as 0.2.0: the changes below break the binary surface of 0.1.0.

### Changed
- `MixtureState`, `PerformanceFigures` and `TransportFigures` expose auto-properties
  instead of public fields and implement `IEquatable<T>` with `==` and `!=`. Code that
  reads or sets them compiles unchanged, but binaries built against 0.1.0 must be
  recompiled.
- The build enables every compiler and analyzer diagnostic and treats each one as an
  error, with no suppression anywhere.

### Added
- The standard exception constructors (parameterless, message, message and inner
  exception) on `DatabaseFormatException`, `AcceleratorUnavailableException`,
  `StateRecordException` and `MixtureMassException`.

### Fixed
- 0.1.0 threw on every CUDA run on GPUs older than Blackwell (compute capability 7.5
  to 9.0): ILGPU 1.5.3 defines the libdevice wrappers itself on those architectures,
  and the post-link, written on the reference machine's Blackwell device alone, read
  the definitions' own parameter names as calls it had no fragment for. The post-link
  now completes only the wrappers ILGPU did not already define. The CUDA binding also
  now loads a probe kernel first, so that a device on which no kernel can load is
  never reported as bound.
- The hidden-defect audit of `Thermo` and `Equilibrium` (2026-09-26):
  - `SpeciesFunctions.RecordLow`/`RecordHigh` now take the lowest lower bound and the
    highest upper bound over every interval of a species, matching NASA CEA's
    `minval`/`maxval` rule, instead of only the first and last interval's own bounds.
    Eleven condensed records begin with an inverted first interval (`300` down to a
    lower temperature); the prior rule wrongly excluded the gap between the inverted
    interval's low end and its own upper bound from the species' range.
  - The condensed-species rules now leave a record with no lower bound when its
    lowest lower bound equals the equilibrium solver's gas-data floor (200 K) and no
    record of its formula adjoins it below (`H2O(cr)` in the committed database),
    instead of quietly treating that floor as a real physical bound.
  - `ScratchLayout.MaxCondensedInSolution` rises from 8 to `TableLimits.MaxElements`
    (20): a state with more stable condensed phases than the prior 8-slot limit could
    represent silently dropped the excess and reported a false `Ok`.
  - The Ok-exit honesty check (the exit guard) now scans every condensed record whose
    elements are present, not only the anti-cycling rule's stood-down records, so a
    positive-gain candidate left out only because the condensed set was full can no
    longer pass as a false `Ok`.
  - `EquilibriumSolver.SolveFrozen` now validates every mole number, gaseous and
    condensed, as finite and non-negative, with a positive gas-phase sum, before
    reporting `Ok`.
  - A warm start whose last step carried a gas across the trace threshold ended
    `NotConverged`; the convergence verdict now covers the gases the step leaves above
    the threshold.
  - Results change only where a phase was wrongly excluded before (the nine condensed
    records between 298.15 K and 300 K, ice below 200 K, states with more than eight
    stable condensed phases). No result of the reference fixtures moved.
- The hidden-defect audit of `Execution` (2026-09-26):
  - A CUDA library present but unusable (a corrupt install, a foreign architecture, a
    missing dependency) left a raw, unwrapped exception and leaked about 190 MiB of
    device memory per attempt, because ILGPU's own accelerator constructor created the
    CUDA context before loading the library. The choice now loads and checks libnvvm and
    the bitcode itself first, so a bad library never reaches the device.
  - The CPU accelerator always ran 16 threads, ILGPU's predefined default, regardless of
    the machine's actual core count. It now sizes itself from `Environment.ProcessorCount`,
    to the nearest multiple of 4 not above it (unchanged on the reference machine's 16
    cores).
  - A batch whose per-case device buffers were sized above roughly 16 GiB with a very
    large `EngineOptions.ScratchBytes` and `ChunkSize` could make a kernel's 32-bit
    offset arithmetic wrap and index outside its own buffer. A chunk's size is now also
    capped so that no buffer's offset can overflow.
  - A driver or libnvvm failure log kept the NUL padding of ILGPU's own buffer in the
    exception message. `Engine.Upload` left the species buffers of a failed upload live
    until the engine itself was disposed, when only the transport table's own upload
    failed. `EngineOptions.LibNvvmPath` given without `LibDevicePath`, or the reverse,
    silently fell through to discovery; it is now an `ArgumentException` naming the
    missing option. Each is fixed.
- The hidden-defect audit of `Transport` (2026-09-26):
  - When two of a station's transport-set components shared a proportional
    stoichiometry column, the row reduction zeroed the second component's own pivot
    but kept it as a component; every reaction then took coefficients from an
    unreduced row and failed to conserve the elements, with a plausible but wrong
    reacting conductivity and heat capacity under status `Ok`. The row now reverts to
    its default species before reducing, as NASA CEA does, and is left unreduced only
    when that pivot is zero too.
  - A species with viscosity fits but no conductivity fit (`UF6`, the only such
    species in the committed `trans.inp`) was estimated in its conductivity but not
    counted in `TransportFigures.EstimatedSpeciesCount` or `EstimatedMoleFraction`. A
    species now counts as estimated when either figure is estimated.
- Under WSL, the second CUDA engine created in a process failed: ILGPU 1.5.3 installs
  a `DllImportResolver` on its own assembly every time it binds CUDA there, and .NET
  allows only one per assembly. The engine now falls back to registering the devices
  itself when that happens, so every CUDA context of a process binds, not only the
  first.
- The hidden-defect audit of `Performance` (2026-09-26):
  - The throat search now tracks a bracket of the smallest-subsonic and
    largest-supersonic pressures seen during the momentum iterations and, where the
    twenty iterations end without the sonic point, bisects that bracket in ln p; at a
    melting plateau's edge, where u²/a² jumps across 1 instead of crossing it
    continuously, the throat is accepted as the state at the plateau's high-pressure,
    single-phase side instead of ending `ThroatNotFound`.
  - A rocket exit's area ratio must now be strictly greater than 1: exactly 1 is
    `AreaRatioInvalid`, matching the reference's own rule.
  - An area-ratio exit is accepted only when its last pass was supersonic; a station
    whose iteration ends on the subsonic side of the sonic point is `NotConverged`
    even if an earlier pass of the same iteration had converged.
  - A flow model other than the three named values is now `InvalidInput` for the
    whole case; such values were solved as frozen at the throat.
  - The throat's `PressureRatio`, and what the exit stations start from, now come
    from the pressure of the state actually solved, not a pressure one momentum step
    past it.
  - Every station downstream of the chamber that the node accepts must now keep the
    chamber's entropy to a relative `1e-9`; a station whose converged entropy drifts
    beyond that is `NotConverged` instead of a silently accepted isentropic-expansion
    violation.

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

[Unreleased]: https://github.com/baryon-asymm/APThermo/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/baryon-asymm/APThermo/releases/tag/v0.1.0
