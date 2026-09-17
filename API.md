# API.md — APThermo

Tree root. The system is a .NET library under the root namespace
`APThermo`, whose front door is the `Problems` node, plus a
command-line tool over it. Everything not named here is internal and may change.

## How the system is used

1. Load the NASA database once (`Data` node). Either take the copy embedded in the
   package, or read files from a path supplied by the caller.
2. Describe a propellant (`Problems` node): reactants by database name or by formula
   and enthalpy, amounts by mass fraction, by moles or by oxidizer-to-fuel ratio,
   reactant temperatures where they differ from the records' own; or hand over a
   mixture by its element moles and enthalpy per kilogram.
3. Describe the problem: chamber pressure, area ratios or pressure ratios, flow model
   and freezing station, transport on or off; or an equilibrium state at assigned
   pressure with the temperature, the enthalpy or the entropy given; or a batch of
   such problems, or a list of state records.
4. Create a solver (`Problems` node) bound by the execution options to the CPU
   accelerator or to CUDA, and solve. The result holds the station states, the
   compositions by name and the performance figures, or a batch of them.

The command line does the same with JSON files (`Cli`).

## Entry points ✅

```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;

var database = SpeciesDatabase.LoadBundled();                               // Data node; or SpeciesDatabase.Load(thermoPath, transPath)

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
$ apthermo schema input                                      # a JSON Schema of the document shapes
```

`apthermo` is the tool command name of the `Cli` node's package; a direct run is
`dotnet APThermo.Cli.dll …`. Exit codes: 0 every case ok, 1 a
case failed numerically (document written), 2 invalid input, 3 accelerator or
infrastructure error. The JSON Schemas of the document shapes are embedded in the
tool; `apthermo schema <name>` prints one of the five (`input`, `output`, `states`,
`species`, `devices`).

Pressures in Pa, temperatures in K, specific impulse in m/s; every other unit is SI as
stated in `BOOT.md`. A failed case is reported with a status, never with a partially
filled result.

## Children ✅

- [Problems](./src/Problems/API.md) — the front door: propellants, problems, results, the solver.
- [Cli](./src/Cli/API.md) — the `apthermo` command line: JSON in, JSON or CSV out.
- [Data](./src/Data/API.md) — the NASA databases as an object model: the embedded database or database files, and queries over them.
- [Execution](./src/Execution/API.md) — the accelerator options and their description (`EngineOptions`, `AcceleratorProbe`). The engine and its batches are its tree contract, and consumers run them through `Problems`.
- [Samples](./samples/Samples/API.md) — the consumer scenarios as running programs over the package surface; the source of the guide's C# blocks. Not packed, part of no package.

Nodes whose package surface is only the vocabulary that problems and results use
(`CaseStatus`, `MixtureState`, `ProblemKind`, `FlowModel`, `PerformanceFigures`,
`TransportFigures`), their formulas internal: [Thermo](./src/Thermo/API.md),
[Equilibrium](./src/Equilibrium/API.md), [Performance](./src/Performance/API.md),
[Transport](./src/Transport/API.md).

⚠ 2026-09-15 (distribution phase): this list said that `Execution` is "used directly by
advanced callers who build batches themselves". It also called the four nodes above
"internal nodes, not used from outside the tree". The review of the package surface
found two things: consumers name those four nodes' result structs and enums, and no
consumer scenario needs the batch path. That path became internal, and `Solver` is the
batch entry (root `BOOT.md`, `## Delivery`, Tree contracts). The database loading
above read files only until the NASA files were embedded the same day.

## Test nodes ✅

- [Fixtures](./tests/Fixtures/API.md) — the reference outputs, their generator, the tolerance table;
  [Fixtures.Tests](./tests/Fixtures.Tests/API.md) proves their form and provenance.
- [Data.Tests](./tests/Data.Tests/API.md), [Thermo.Tests](./tests/Thermo.Tests/API.md),
  [Equilibrium.Tests](./tests/Equilibrium.Tests/API.md), [Performance.Tests](./tests/Performance.Tests/API.md),
  [Transport.Tests](./tests/Transport.Tests/API.md), [Execution.Tests](./tests/Execution.Tests/API.md),
  [Problems.Tests](./tests/Problems.Tests/API.md), [Cli.Tests](./tests/Cli.Tests/API.md) — what each node proves.
- [Harness](./tests/Harness/API.md) — the scaffolding the test nodes share: a CPU host, bit comparison, bit snapshots, fixture families.
- [Benchmarks](./tests/Benchmarks/API.md) — the speed benchmarks (BenchmarkDotNet), run by hand; figures recorded, never asserted.
- [Docs.Tests](./tests/Docs.Tests/API.md) — the documentation tests: the guide's snippets against the samples, the approved outputs of the samples and of the command-line examples, the links, the schemas, the guide pages' shape.
- [Protocol.Tests](./tests/Protocol.Tests/API.md) — the documents against the code (AGENTS.md §13) and the root invariants that need reflection.
