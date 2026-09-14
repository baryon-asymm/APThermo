# API.md — AerospacePropellantThermodynamics

Tree root. The system is a .NET library under the root namespace
`AerospacePropellantThermodynamics`, whose front door is the `Problems` node, plus a
command-line tool over it. Everything not named here is internal and may change.

## How the system is used

1. Load the NASA database once (`Data` node) from `data/` or from a path supplied by
   the caller.
2. Describe a propellant (`Problems` node): reactants by database name or by formula
   and enthalpy, amounts by mass fraction, by moles or by oxidizer-to-fuel ratio,
   reactant temperatures where they differ from the records' own; or hand over a
   mixture by its element moles and enthalpy per kilogram.
3. Describe the problem: chamber pressure, area ratios or pressure ratios, flow model
   and freezing station, transport on or off; or an equilibrium state at assigned
   pressure with the temperature, the enthalpy or the entropy given; or a batch of
   such problems, a sweep over ratios and pressures, or a list of state records.
4. Create a solver (`Problems` node) bound by the execution options to the CPU
   accelerator or to CUDA, and solve. The result holds the station states, the
   compositions by name and the performance figures, or a batch of them.

The command line does the same with JSON files (`Cli`).

## Entry points ✅

```csharp
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Problems;

var database = SpeciesDatabase.Load(thermoPath, transPath);                 // Data node

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)                                  // K
    .Fuel("H2(L)", temperature: 20.27)
    .OxidizerToFuelRatio(6.0)
    .Build();

var problem = new RocketProblem
{
    ChamberPressure = 7.0e6,                                                // Pa
    AreaRatios = [20.0, 77.5],
    Flow = FlowModel.ShiftingEquilibrium,                                   // or FrozenAtChamber, FrozenAtThroat
    Transport = true,
};

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Auto });
RocketResult result = solver.Solve(propellant, problem);                    // one case
IReadOnlyList<RocketResult> results = solver.Solve(propellant, problems);   // one batch
IReadOnlyList<RocketResult> sweep = solver.Solve(new RocketSweep(propellant, ratios, pressures, areaRatios));
EquilibriumResult state = solver.Solve(propellant, new EquilibriumProblem { Pressure = 7.0e6 });   // hp at the propellant's enthalpy
IReadOnlyList<EquilibriumResult> states = solver.SolveStates(records);      // element moles, pressure and one target each
```

⚠ 2026-09-12: the sketch solved on an `Engine` of the execution node
(`engine.Solve(propellant, problem)`) and froze the flow with a `Frozen(FreezeAt.Chamber)`
call. The solver lives in the front door node, because turning a propellant into a
chemical system is its knowledge and the engine only runs batches; the flow models
are the performance node's enum. Equilibrium problems, sweeps and state records
were part of the intent from the start and are shown now that they exist.

## Command line ✅

```console
$ apthermo rocket problem.json --output result.json          # chamber, throat, exits; JSON or --format csv
$ apthermo equilibrium problem.json                          # one tp, hp or sp state
$ apthermo states records.json --transport                   # state records of another simulation, one batch
$ apthermo species --find H2O                                # the database
$ apthermo devices                                           # the accelerators
```

`apthermo` is the tool command name of the `Cli` node's package; a direct run is
`dotnet AerospacePropellantThermodynamics.Cli.dll …`. Exit codes: 0 every case ok, 1 a
case failed numerically (document written), 2 invalid input, 3 accelerator or
infrastructure error.

Pressures in Pa, temperatures in K, specific impulse in m/s; every other unit is SI as
stated in `BOOT.md`. A failed case is reported with a status, never with a partially
filled result.

## Children ✅

- [Problems](./src/Problems/API.md) — the front door: propellants, problems, results, the solver.
- [Cli](./src/Cli/API.md) — the `apthermo` command line: JSON in, JSON or CSV out.
- [Data](./src/Data/API.md) — the NASA databases as an object model (used directly to load and query the database).
- [Execution](./src/Execution/API.md) — engines, accelerators, batches (used directly by advanced callers who build batches themselves).

Internal nodes, not used from outside the tree: [Thermo](./src/Thermo/API.md),
[Equilibrium](./src/Equilibrium/API.md), [Performance](./src/Performance/API.md),
[Transport](./src/Transport/API.md).

## Test nodes ✅

- [Fixtures](./tests/Fixtures/API.md) — the reference outputs, their generator, the tolerance table;
  [Fixtures.Tests](./tests/Fixtures.Tests/API.md) proves their form and provenance.
- [Data.Tests](./tests/Data.Tests/API.md), [Thermo.Tests](./tests/Thermo.Tests/API.md),
  [Equilibrium.Tests](./tests/Equilibrium.Tests/API.md), [Performance.Tests](./tests/Performance.Tests/API.md),
  [Transport.Tests](./tests/Transport.Tests/API.md), [Execution.Tests](./tests/Execution.Tests/API.md),
  [Problems.Tests](./tests/Problems.Tests/API.md), [Cli.Tests](./tests/Cli.Tests/API.md) — what each node proves.
- [Harness](./tests/Harness/API.md) — the scaffolding the test nodes share: a CPU host, bit comparison, bit snapshots, fixture families.
- [Protocol.Tests](./tests/Protocol.Tests/API.md) — the documents against the code (AGENTS.md §13) and the root invariants that need reflection.
