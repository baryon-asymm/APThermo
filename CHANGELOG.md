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

The hidden-defect audit of 2026-09-26 (`Data`, `Problems` and `Cli`):

- The CSV output's input columns carry the prefix `inputs.`, so an input never
  repeats a station field's name. `equilibrium`'s and `states`' `pressure`,
  `temperature`, `enthalpy` and `entropy` used to collide with the state's own
  fields of the same name; a rocket record's exit row silently read back the exit
  pressure under the chamber's column.
- A JSON document (a problem or a state record) with a member given twice is refused
  with exit 2, naming the field and its path, instead of silently keeping the last
  value.
- `--output=` and `--database=` (an option given an empty value) are exit 2 naming
  the option, instead of an unhandled exception (`--output=`) or silently reading
  `thermo.inp` from the working directory (`--database=`).
- A refusal of a state record (a bad pressure, temperature, exit or target, or an
  element the database lacks) names the record's own file and line or JSON Lines
  line, instead of its position inside the batch the runner built internally.
- An element with no candidate species is refused by name, instead of solving with a
  candidate list that silently omits the element or failing with an unrelated
  numerical status; an element with no monatomic record to take its atomic weight
  from is refused with a message that says so.
- A propellant's oxidizer, fuel or named-reactant group that mixes mass-fraction and
  mole amounts is refused by name, instead of summing the two as if they were the
  same unit.
- A reactant name with several database records (a joined-table product species
  such as a multi-range condensed species, or a reactant-only name with several
  records, such as `n-Butanol`) now takes the union of every record's temperature
  range, and a reactant-only name with several records resolves to the last one, as
  the NASA `cea` package does; both used to take only the first record.
- `--mass-tolerance` is refused (exit 2) on a document whose propellant is given by
  reactants, since the option applies only to a document that carries element moles
  directly (`states`, `propellant.elementMoles`); such a reactant propellant's own
  mass refusal now names `the propellant's mixture (case i)`, and `run.massTolerance`
  records the fixed default the front door holds it to, instead of echoing the
  option's value.
- A malformed database line is reported with exactly one `file:line:` prefix,
  instead of the command line prefixing a message from `Data` that already carried
  one.
- A species formula pair with a populated symbol and a zero count is dropped when
  the database file is read, matching the documented rule; only a pair with an
  empty symbol was dropped before.
- `Station.TransportStatus`'s documentation now says it is null when transport was
  not requested *or the station did not converge*; the code already did this, and
  the wording is the only thing that changed.

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
