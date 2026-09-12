# API.md — AerospacePropellantThermodynamics

Tree root. The system is a .NET library under the root namespace
`AerospacePropellantThermodynamics`, whose front door is the `Problems` node, plus a
command-line tool over it. Everything not named here is internal and may change.

## How the system is used ⏳

1. Load the NASA database once (`Data` node) from `data/` or from a path supplied by
   the caller.
2. Describe a propellant (`Problems` node): reactants by database name, amounts by mass
   fraction or by oxidizer-to-fuel ratio, reactant temperatures or enthalpies.
3. Describe the problem: chamber pressure, expansion ratios or exit pressures, flow
   model and freezing station; or a batch of such problems for one propellant.
4. Create an engine bound to an accelerator (CPU or CUDA, `Execution` node) and solve.
   The result holds the station states and the performance figures, or a batch of them.

The command line does the same with JSON files.

## Entry points ⏳

```csharp
namespace AerospacePropellantThermodynamics.Problems;

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
    Flow = FlowModel.ShiftingEquilibrium,                                   // or Frozen(FreezeAt.Chamber)
};

using var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Auto });
RocketResult result = engine.Solve(propellant, problem);                    // one case
RocketResult[] results = engine.Solve(propellant, problems);                // one batch
```

```console
$ apthermo rocket problem.json --output result.json
```

Pressures in Pa, temperatures in K, specific impulse in m/s; every other unit is SI as
stated in `BOOT.md`. A failed case is reported with a status, never with a partially
filled result.

## Children ⏳

- [Problems](./src/Problems/API.md) — the front door: propellants, problems, results, the solver.
- [Cli](./src/Cli/API.md) — the `apthermo` command line: JSON in, JSON or CSV out.
- [Data](./src/Data/API.md) — the NASA databases as an object model (used directly to load and query the database).
- [Execution](./src/Execution/API.md) — engines, accelerators, batches (used directly by advanced callers who build batches themselves).

Internal nodes, not used from outside the tree: [Thermo](./src/Thermo/API.md),
[Equilibrium](./src/Equilibrium/API.md), [Performance](./src/Performance/API.md),
[Transport](./src/Transport/API.md).

## Test nodes ⏳

- [Fixtures](./tests/Fixtures/API.md) — the reference outputs, their generator, the tolerance table;
  [Fixtures.Tests](./tests/Fixtures.Tests/API.md) proves their form and provenance.
- [Data.Tests](./tests/Data.Tests/API.md), [Thermo.Tests](./tests/Thermo.Tests/API.md),
  [Equilibrium.Tests](./tests/Equilibrium.Tests/API.md), [Performance.Tests](./tests/Performance.Tests/API.md),
  [Transport.Tests](./tests/Transport.Tests/API.md), [Execution.Tests](./tests/Execution.Tests/API.md),
  [Problems.Tests](./tests/Problems.Tests/API.md), [Cli.Tests](./tests/Cli.Tests/API.md) — what each node proves.
- [Protocol.Tests](./tests/Protocol.Tests/API.md) — the documents against the code (AGENTS.md §13).
