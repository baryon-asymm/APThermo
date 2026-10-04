# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- `CaseStatus.NoGasPhase` (value 8, `noGasPhase` in the command line's documents): the
  equilibrium holds no gas phase, proven by a tangent-plane certificate (a mixture of
  condensed species alone, as KO2 below its melting point). `Moles` hold the condensed
  minimum with every gas zero and `Multipliers` the certificate's. `State` carries the
  temperature and the pressure only and every other field is 0; for an hp or sp case the
  temperature is the one the search found. The command line exits 1 for it, as for any
  failed case. Before, such a state ended `NotConverged` or `SingularMatrix`, or an hp or
  sp state `TemperatureOutOfRange`. A known limitation of 0.2.1.

### Changed
- On any status but `Ok` the state is zero, as before, with the one exception of
  `NoGasPhase`, whose state is the temperature and the pressure.
- An `Ok` state whose gas is 1e-6 of the mixture or less, and whose gas composition had converged
  only to about 5e-8, is now converged further; its composition moves at that level.
- Every `Ok` state closes each element's balance to 1e-13 of that element's abundance. Before, the
  bound was 1e-12 kmol/kg, which for a real mixture is 6e-11 of the carbon of MgCO3, and a state
  could stop with one trace carrier (1.6e-13 kmol/kg of CO) outside the balance: the hp states of
  MgCO3 under CO2 on its decomposition plateau closed carbon to 1.4e-11 of its abundance and now
  close to 1e-14. The one tp state of the 17-element fixture that closed chlorine to 7.5e-13
  reports one more species (NO, 2.4e-14 of the gas) and closes to 4e-16.

### Fixed
- An assigned-temperature state whose gas is a trace beside condensed species, or one of whose
  element combinations only trace gases carry, no longer ends `NotConverged`. Examples:
  - MgCO3 under CO2 at 10 MPa between 700 and 845 K;
  - CaCO3 or MgCO3 with a trace of excess CO2 just below decomposition;
  - Al2O3 with a trace of excess oxygen at 1 000–3 000 K;
  - KCl with a trace of excess chlorine.

  When the gas-phase test finds that a gas is required, a second iteration that carries the gas
  composition exactly converges it.
- An assigned-enthalpy or assigned-entropy state with a trace gas no longer ends `Ok` with a gas
  composition that had not converged. With 1e-6 excess CO2 beside MgCO3, CO was reported at up to
  2e-5 of the gas where the equilibrium holds none. Every `Ok` now has each reported gas on its
  chemical potential within 1e-9, and such a state is solved by the temperature search with the
  trace-gas iteration as its last step.
- An assigned-enthalpy or assigned-entropy state whose target lies at a temperature where the NASA
  fits of two ranges meet (1 000 K for most species) ends `Ok` there. The two fits disagree by up to
  1e-8 in ln x of a gas, so no temperature has the target within the iteration's tolerance and the
  gas-level check of every `Ok` could not hold on both sides. The state reported is the equilibrium at
  the junction on the side whose enthalpy (entropy) is nearer the target, and it misses the target by
  at most the jump of the data, 1e-8 of c_p T (measured 8.3e-9 for gas mixtures at 1 000 K).
- An assigned-temperature state whose mixture differs from a condensed assemblage by a trace of one
  element, so that the gas phase is required but the condensed phase-one point already fills the elements
  (KCl with 1e-10 too little chlorine at 1 200 K, Al(OH)3 with 1e-12 too little oxygen at 300 K), no
  longer ends `NotConverged`.
- A mixture of KO2 with 1e-10 too little oxygen no longer ends `NoGasPhase` with its second condensed
  record (K2O, 9.4e-13 kmol/kg) left out of the composition and 1.3e-10 of the potassium unaccounted for.
  The verdict's bound itself is unchanged, 1e-12 kmol/kg: a mixture within 1e-12 of exact stoichiometry
  still ends `NoGasPhase`, and an excess of that size is not held by the reported moles.
- Assigned-enthalpy and assigned-entropy states of MgCO3 under CO2 below its decomposition no longer
  end `TemperatureOutOfRange` when their derivative system was singular.
- An assigned-enthalpy or assigned-entropy state the iteration cannot reach from its start
  (much condensed water in the state, a gas-participating plateau of CaCO3 or MgCO3, the
  AP/HTPB/Al states near 400 K that failed in 0.2.1) is now found by a search on the
  temperature: tp states at assigned temperatures bracket the target, and the case ends
  `Ok` at the temperature of the state, `NoGasPhase` where the state holds no gas, or
  `TemperatureOutOfRange` where no state of the species list has the target.
- A cold assigned-enthalpy or assigned-entropy state below the lower bound of a condensed
  record whose data stop there (`H2O(L)` from 273.15 K without `H2O(cr)`, `C(gr)` from
  300 K) no longer reports the supercooled vapour as `Ok` when a state of the same
  enthalpy or entropy holding the phase exists above the bound: the equilibrium is the
  one of higher entropy (hp) or lower enthalpy (sp). A silent wrong answer of 0.2.1 and
  earlier.
- A state on a plateau where the gas takes part (boiling water, CaCO3 and CaO under CO2,
  NH4Cl, Ca(OH)2, MgCO3), whose plateau temperature moves with the pressure, no longer
  ends `TemperatureOutOfRange` or `NotConverged` after its composition has converged. An
  isentrope through such a region (a nozzle expanding nearly pure water vapour into its
  two-phase region) now solves at every station. On such a plateau `CpEquilibrium`,
  `CvEquilibrium` and `DlnVdlnT` are 0 as on the other plateaus, `GammaS` and `SoundSpeed`
  follow the isentrope along the moving plateau, `DlnVdlnP` carries the isentropic
  derivative, and `GammaS` may fall below 1. A known limitation of 0.2.1.
- Beside such a plateau, where the composition is only nearly univariant, `GammaS` was
  wrong by up to 0.17 % with an `Ok` status. It now comes from the isentrope as well.

### Notes
- An assigned-temperature state below a dead-end bound (the cases above) still reports the
  supersaturated gas: the species list is the contract, a record outside its data range is
  no candidate, and NASA CEA does the same. The library adds no species to a list, so an
  `Only` list without `H2O(cr)` cannot hold water below 273.15 K; an hp or sp target that
  no state of the list reaches ends `TemperatureOutOfRange`. The 0.2.1 limitation about a
  reactant set with liquid water and no ice candidate is superseded by the two entries
  above and this note.

## [0.2.1] - 2026-10-03

### Fixed
- An assigned-temperature state whose retained gases tie three elements together (only
  CO2, H2O and N2 at low temperature, where the oxygen row equals a combination of the
  carbon and hydrogen rows) no longer ends `SingularMatrix`: the element tie now takes
  any linear combination of element rows, not only a pair. A known limitation of 0.2.0.
- Alkali perchlorate states where two trace species swapped places across the retention
  threshold at every step (KClO4 near 610–680 K, NaClO4 near 490–500 K) no longer end
  `NotConverged`: the retention verdict binds only the second threshold stage, the switch
  to it restarts the polish, and two consecutive swaps hold the retained set. A known
  limitation of 0.2.0.
- A state whose condensed species are linearly dependent only together with the gas
  phase (KO2 entering beside K2O2(cr) and an all-O2 gas, KO2 being half of each, at
  K:Cl:O = 1:0.9:4) no longer cycles to
  `NotConverged`: the dependency test counts the gas phase as one more column.
- An assigned-enthalpy or assigned-entropy state inside a reaction plateau between
  different condensed species (Al(OH)3, Al2O3 and liquid water at 415.948 K near 7 MPa)
  no longer ends `SingularMatrix` when the solve starts near the plateau: the derivative
  properties treat any linearly dependent set of condensed species as melting plateaus
  were already treated. A known limitation of 0.2.0.

### Changed
- The rocket throat is found by a decision at `|u²/a² − 1| ≤ 1e-8` followed by exactly two
  more momentum steps, instead of iterating to `1e-10`. Throat and exit figures move at
  the twelfth significant digit; the CUDA and CPU accelerators now stop on the same step
  far more often, so their results agree as the GPU/CPU tolerance table requires.
- The guide's custom-propellant example defines HTPB as in Thomas and Petersen, AIAA
  Journal 2021 (doi:10.2514/1.J060972): C 213.8 H 323.0 O 4.6 N 2.3, +342 kJ/mol. The
  earlier definition had no source. The guide notes that published HTPB heats of
  formation vary widely and change equilibrium results by up to 5 %.
- On a reaction plateau the state reports the plateau convention it already reported on
  a melting plateau: `CpEquilibrium`, `CvEquilibrium` and `DlnVdlnT` are 0 and `GammaS`
  is `−1/DlnVdlnP`. NASA CEA 3.3.4 reports frozen values there instead.

### Known limitations
Known and not fixed in this release:
- An assigned-enthalpy state on the AP/HTPB/Al table near 400 K fails from a cold
  start. NASA CEA fails there too. A state warm-started from above the
  Al(OH)3/Al2O3/H2O(L) plateau fails when it lies 25 K or more below it.
- A univariant equilibrium in which the gas takes part (CaCO3 and CaO under pure CO2),
  whose plateau temperature depends on the pressure, is not covered by the plateau
  convention; its derivative properties there are not verified.
- At the exact stoichiometry of KO2 or NaO2 (no chlorine, two oxygen atoms per alkali
  atom) the gas phase vanishes and the solve ends `NotConverged`. NASA CEA does not
  converge there either.
- A reactant set with liquid water and no ice candidate (as in RP-1311 example 12)
  cannot place an assigned-enthalpy or assigned-entropy state below 273.15 K. The
  solve lands on supercooled vapour or ends `TemperatureOutOfRange`.

## [0.2.0] - 2026-09-30

This release breaks the binary surface of 0.1.0: code built against 0.1.0 must be
recompiled. It fixes the findings of two hidden-defect audits of the whole library, the
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
    and mole amounts is refused, instead of summing the two as one unit; without an
    oxidizer-to-fuel ratio this now applies across every group of the propellant, not
    only within one, since the whole propellant is then one unit of normalization;
  - an element with no candidate species, or with no monatomic record to take its
    atomic weight from, is refused, instead of being silently dropped or ending in an
    unrelated numerical status; this now applies only when some mixture of the batch
    gives the element a nonzero abundance, so an element listed at zero everywhere
    (as a plasma-capable code writes a neutral mixture) is masked, as an absent
    element already is; the refusal names the `Only` or `Omit` list as its cause when
    one excluded the element, and an `Only` name that is an ionized species or an
    inert pseudo-element record is refused by name;
  - `SolveStates` and `SolveRocketStates` now check each record's own elements before
    the union of the batch is built, so an unknown element or a candidate-less
    abundance is refused by that record's own index, not the union's; a condition of
    the batch itself (transport requested without `trans.inp`, an invalid mass
    tolerance) is refused naming the option instead of blaming the batch's first
    record;
  - a rocket's temperature estimate and an equilibrium problem's assigned temperature
    of positive or negative infinity are refused, as `NaN` and negative values
    already were;
  - a propellant group whose amounts sum to a non-finite mass is refused, naming the
    group, instead of a later, misleading mass share;
  - a multi-record reactant's accepted temperature window now covers the lowest lower
    and the highest upper bound of its own records' intervals; `Br2(cr)` (written
    300 → 265.9 K) can now be used as a reactant, where 0.1.0 and the first 0.2.0
    candidate could not use it at any temperature;
  - `EngineOptions.LibNvvmPath` without `LibDevicePath`, or the reverse, is an
    `ArgumentException` naming the missing option, instead of falling back to
    discovery;
  - an equilibrium state whose temperature ends outside [160 K, 22 000 K], the
    mixture window of NASA CEA, is `TemperatureOutOfRange`, an assigned-temperature
    problem included, and so is a state whose heat capacities, isentropic exponent or
    sound speed are not finite and positive; 0.1.0 reported ice at 60 to 150 K as
    `Ok` with a negative heat capacity or a NaN sound speed;
  - a frozen state at an infinite, NaN or vanishing temperature is refused, as a
    shifting one already was;
  - in flow frozen at the throat, an exit given by a pressure ratio at or above the
    throat's pressure is `InvalidInput`; 0.1.0 reported it `Ok` with an infinite area
    ratio.
- An equilibrium composition now reports every gaseous species above 1e-11 of the
  gas at its converged amount. 0.1.0 zeroed species between 1e-11 and 1e-8 in the
  report after converging with them, so the reported moles did not conserve the
  elements to the last digits; the figures move in their trailing digits only.
- Command line:
  - the CSV output's input columns carry the prefix `inputs.`, so an input never
    repeats a result field's name (`pressure`, `temperature`, `enthalpy` and
    `entropy` used to appear twice, and a rocket record's exit row read back the exit
    pressure under the chamber's column);
  - a JSON document with a member given twice is refused with exit 2, naming the
    field, instead of keeping the last value;
  - an option given an empty or a white-space-only value (`--output=`, `--output " "`,
    `--database=`) is exit 2, instead of an unhandled exception, a file named a single
    space, or reading `thermo.inp` from the working directory;
  - `--mass-tolerance` is refused (exit 2) on a document whose propellant is given by
    reactants, since it applies only to element moles given directly; the refusal now
    carries the document's own path, like every other refusal of the command;
  - a sweep range whose step count is not finite, or whose axis or the document's
    Cartesian product exceeds a declared limit (1,000,000 values per axis, 10,000,000
    cases per document), is refused by its path, instead of an unhandled
    `OverflowException` or `OutOfMemoryException`, or a range of three billion steps
    failing the "ends on a step" test for a false reason;
  - a lone UTF-16 surrogate in a JSON member name or string value is refused naming
    its path, instead of an unhandled exception;
  - a refused state record is named by its own file and line, not by its position in
    an internal batch;
  - the `species` listing's `run` section no longer records a `threshold` or a
    `massTolerance`, options the command does not take.
- The CPU accelerator runs as many threads as the machine has logical cores, rounded
  down to a multiple of 4. 0.1.0 always ran 16. Results do not depend on the count.
- The build enables every compiler and analyzer diagnostic and treats each one as an
  error, with no suppression anywhere.

### Added
- The standard exception constructors (parameterless, message, message and inner
  exception) on `DatabaseFormatException`, `AcceleratorUnavailableException`,
  `StateRecordException` and `MixtureMassException`.

### Fixed
- A transfer between the host and the accelerator could be lost to a .NET garbage
  collection that moved the host array during the copy, on the CPU accelerator and on
  CUDA alike, since 0.1.0. A lost download returned a case with status `Ok` and its
  figures zero, most often the transport figures; a lost upload made the kernel read
  stale memory. Rare in practice (it needs a collection inside the copy), and more
  likely on a busy machine with few cores. Every transfer now pins the host memory,
  and a download that wrote nothing throws `InvalidOperationException` instead of
  returning zeros.
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
  - An engine releases its compiled kernels and the compiler's caches when it is
    disposed, so a disposed `Solver` that stays referenced no longer holds about
    150 MB, and a `Solver`'s first rocket calculation on the CPU compiles in about
    2 s and 0.4 GB instead of 6 s and 1.2 GB.
  - On a GPU that drives a display, the driver kills a launch that runs longer than
    its limit (2 s by default on Windows and under WSL2). Chunks are now sized to a
    quarter of that limit from the measured time per case, and a launch the driver
    kills anyway is an `AcceleratorUnavailableException` that names the limit. One
    case of about 16 or more elements can exceed the limit alone; such systems belong
    on the CPU accelerator or on a GPU without the limit.
  - After such a timeout the engine's context is unusable (NVIDIA documents the error
    as sticky): a further run, upload or math probe on it now refuses immediately,
    naming the earlier timeout, instead of touching the dead context again; disposing
    the timed-out engine or its uploaded tables no longer risks replacing the
    reported failure with a raw, undocumented exception from ILGPU's own cleanup.
- Equilibrium and condensed phases:
  - A condensed species' temperature range now spans all its intervals, as NASA CEA
    reads it; nine condensed records between 298.15 K and 300 K were excluded before.
  - Ice (`H2O(cr)`) may appear below 200 K, the lower end of the gas data, down to
    the 160 K floor of the mixture window, as in NASA CEA.
  - The trace threshold has two stages, as in NASA CEA: 1e-8 of the gas until the
    first convergence, then 1e-11. Low-temperature states such as RP-1311 example 5
    at 300 to 340 K, and AP/HTPB/Al at 300 to 350 K, ended `NotConverged` and now
    converge.
  - A singular Newton matrix now removes the species on its failing row, ties an
    element whose balance is carried by one other element's species, and brings a
    condensed species in by a basis change when the retained set is linearly
    dependent. Salt decompositions (NaClO4, KClO4) and AP/HTPB/Al at 420 to 430 K
    ended `SingularMatrix` and now converge.
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
    cannot exist at the new one. A warm start that fails for any reason is now
    retried from a cold start (at 3 800 K for an hp or sp problem).
  - The frozen composition's mole numbers are validated before `Ok` is reported.
- Rocket performance:
  - At the high-pressure edge of a melting plateau (for example Al2O3 in an
    aluminized propellant), where the sound speed jumps, the throat search ended
    `ThroatNotFound`. The throat is now the first maximum of the mass flux met from
    the chamber, where the flow first chokes, and at a plateau edge its state is the
    single-phase one on the chamber side, with a Mach number below 1. Where the mass
    flux has two maxima around a melting plateau, the throat is the upstream one,
    which the momentum search alone could miss. NASA CEA reports a wrong c* at a
    plateau edge, about 6 % high in the worst measured case.
  - A chamber on a melting plateau, whose isentropic exponent is exactly 1, ended
    `ThroatNotFound` or `Ok` by rounding; it is now solved.
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
  - Two `omit` lists that joined to the same comma-separated text (a product name may
    itself contain a comma) reused one cached species table for what should have been
    two different ones; the cache key no longer joins names with a separator that can
    appear in a name.

### Known limitations
These equilibrium failures are measured, reported by status (never as a wrong `Ok`),
and planned for 0.2.1:
- Two trace species can cross the trace threshold alternately on every step, so the
  solve never completes its final polish and ends `NotConverged`. It strikes
  decompositions of perchlorates such as KClO4 and NaClO4 at some states between about
  500 and 1400 K, depending on pressure and composition.
- When the retained gases tie three elements together (only CO2, H2O and N2 at low
  temperature, where the oxygen row equals a combination of the carbon and hydrogen
  rows), an assigned-temperature state can end `SingularMatrix`.
- An assigned-enthalpy state inside a reaction plateau between different condensed
  species (for example Al(OH)3, Al2O3 and liquid water near 416 K at 7 MPa) ends
  `SingularMatrix`; melting plateaus of one species are solved.

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

[Unreleased]: https://github.com/baryon-asymm/APThermo/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/baryon-asymm/APThermo/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/baryon-asymm/APThermo/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/baryon-asymm/APThermo/releases/tag/v0.1.0
