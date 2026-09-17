# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
