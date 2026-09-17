# Rocket solving

## Purpose

Solve a rocket nozzle problem with APThermo: given a propellant and a set of exits,
compute the station-by-station thermodynamic state (temperature, pressure, density,
enthalpy, entropy, molar mass) and performance figures (specific impulse, thrust
coefficient, characteristic velocity) for the chamber, throat and each nozzle exit.

## When to use

- You need station-by-station results for a given propellant and nozzle geometry.
- You want to compare different area ratios, pressure ratios or chamber pressures for
  the same propellant.
- You need transport properties (viscosity, thermal conductivity, Prandtl numbers) at
  each station.
- Your propellant is not two database reactants at an oxidizer-to-fuel ratio: it has
  a custom reactant, mass fractions instead of a ratio, or species to omit.

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build a propellant from its reactants. The common case is an oxidizer and a fuel
   at an oxidizer-to-fuel ratio, below; [a custom reactant](#a-custom-propellant) and
   mass fractions are further down this page.
3. Define the rocket problem: chamber pressure (Pa), exits (area ratios, pressure
   ratios, or both — pressure-ratio exits are reported first), the flow model
   (`ShiftingEquilibrium`, `FrozenAtChamber` or `FrozenAtThroat`), and whether to
   compute transport properties.
4. Create a solver bound to the CPU accelerator (see [GPU acceleration](gpu.md) for
   CUDA) and call `Solve`. Check every station's `Status` before reading its figures:
   a failed station carries a status and a zero state, never a partial one.

<!-- snippet: RocketSolveUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
```

`output` is any `TextWriter` (`Console.Out` in a console application):

<!-- snippet: RocketSolve -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)   // K
    .Fuel("H2(L)", temperature: 20.27)        // K
    .OxidizerToFuelRatio(6.0)
    .Build();

var problem = new RocketProblem
{
    ChamberPressure = 7.0e6,                  // Pa
    AreaRatios = [20.0, 77.5],
    Flow = FlowModel.ShiftingEquilibrium,
    Transport = true,
};

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
RocketResult result = solver.Solve(propellant, problem);

void PrintStation(Station station)
{
    if (station.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  {station.Name,-8}  FAILED: {station.Status}");
        return;
    }

    var temperature = station.State.Temperature;
    var pressure = station.State.Pressure / 1e6;
    output.WriteLine(station.Name == "chamber"
        ? $"  {station.Name,-8}  T={temperature:F1} K   P={pressure:F3} MPa"
        : $"  {station.Name,-8}  T={temperature:F1} K   P={pressure:F3} MPa   Isp={station.Performance!.Value.SpecificImpulse:F1} m/s");
}

output.WriteLine($"LOX/LH2 O/F=6.0  Pc={problem.ChamberPressure / 1e6:F1} MPa  status={result.Status}");
foreach (Station station in result.Stations)
{
    PrintStation(station);
}

Station throat = result.Stations[1];
if (throat.Status == CaseStatus.Ok)
{
    if (throat.Transport is { } transport)
    {
        output.WriteLine($"  throat viscosity = {transport.Viscosity:E3} Pa·s");
    }

    foreach (var (species, fraction) in throat.MoleFractions.OrderByDescending(kv => kv.Value).Take(3))
    {
        output.WriteLine($"  {species,-6} x={fraction:F4}");
    }
}
```

### Pressure-ratio exits and a frozen flow

`AreaRatios` and `PressureRatios` may both be given; pressure-ratio exits are
reported before the area-ratio ones. `FlowModel.FrozenAtThroat` freezes the
composition found at the throat for every exit downstream of it:

<!-- snippet: RocketExitsUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
```

<!-- snippet: RocketExits -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)   // K
    .Fuel("H2(L)", temperature: 20.27)        // K
    .OxidizerToFuelRatio(6.0)
    .Build();

var problem = new RocketProblem
{
    ChamberPressure = 7.0e6,   // Pa
    PressureRatios = [10.0],   // p_c / p_e, reported first
    AreaRatios = [50.0],       // A / A_t, reported after
    Flow = FlowModel.FrozenAtThroat,
};

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
RocketResult result = solver.Solve(propellant, problem);

void PrintStation(Station station)
{
    if (station.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  {station.Name,-8}  FAILED: {station.Status}");
        return;
    }

    var temperature = station.State.Temperature;
    output.WriteLine(station.Name == "chamber"
        ? $"  {station.Name,-8}  T={temperature:F1} K"
        : $"  {station.Name,-8}  T={temperature:F1} K   Isp={station.Performance!.Value.SpecificImpulse:F1} m/s");
}

output.WriteLine($"LOX/LH2 O/F=6.0  Pc={problem.ChamberPressure / 1e6:F1} MPa  status={result.Status}");
foreach (Station station in result.Stations)
{
    PrintStation(station);
}
```

### A custom propellant

A propellant may mix database reactants named by total mass fraction (`Named`, no
oxidizer-to-fuel ratio) with a custom reactant given by its formula and enthalpy of
formation, and may omit a species the database would otherwise consider. This is the
aluminized composite propellant of the fixtures (`tests/Fixtures/cases/rocket`):

<!-- snippet: CustomPropellantUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
```

<!-- snippet: CustomPropellant -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var binder = new CustomReactantDefinition(
    Formula: [new ElementCount("C", 7.3165), new ElementCount("H", 10.3416), new ElementCount("O", 0.0674)],
    Enthalpy: -1046.0,        // J/mol at Temperature
    Temperature: 298.15);     // K

var propellant = Propellant.From(database)
    .Named("NH4CLO4(I)", massFraction: 0.68)
    .Custom(Reactant.Custom("HTPB", binder, ReactantRole.Named, amount: 0.14))
    .Named("AL(cr)", massFraction: 0.18)
    .Omit("AL(L)")
    .Build();

var problem = new RocketProblem
{
    ChamberPressure = 7.0e6,   // Pa
    AreaRatios = [8.0, 12.0],
    Flow = FlowModel.FrozenAtThroat,
};

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
RocketResult result = solver.Solve(propellant, problem);

output.WriteLine($"AP/HTPB/Al  Pc={problem.ChamberPressure / 1e6:F1} MPa  status={result.Status}");
foreach (Station station in result.Stations)
{
    if (station.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  {station.Name,-8}  FAILED: {station.Status}");
        continue;
    }

    var temperature = station.State.Temperature;
    output.WriteLine(station.Name == "chamber"
        ? $"  {station.Name,-8}  T={temperature:F1} K"
        : $"  {station.Name,-8}  T={temperature:F1} K   Isp={station.Performance!.Value.SpecificImpulse:F1} m/s");
}
```

## Errors

- **Unknown reactant name**: `KeyNotFoundException` at `Build`, before any solve is
  attempted.
- **Invalid mixture rule**: an oxidizer-to-fuel ratio without at least one oxidizer
  and one fuel, oxidizers and fuels mixed without a ratio, or a named reactant given
  alongside a ratio: `ArgumentException` at `Build`.
- **Reactant temperature out of range**: a temperature outside its database record's
  polynomial intervals (widened by `PropellantBuilder.TemperatureMargin`, 10 K):
  `ArgumentException` naming the reactant and its valid range, at `Build`.
- **Non-positive chamber pressure or exit value**: `ArgumentException` at `Solve`,
  before any kernel runs.
- **A station fails numerically** (no physical solution at that station, the Newton
  iteration does not converge, a singular derivative matrix, an hp chamber search
  leaving the database's temperature range): the station carries a `CaseStatus`
  other than `Ok` and a zero state; the run itself does not throw. See
  [Troubleshooting](troubleshooting.md) for the full list of statuses.

## See also

- [Batch solving](batch.md) — many rocket problems, or an oxidizer-to-fuel sweep, in one call
- [Equilibrium states](equilibrium.md) — a single equilibrium state without a nozzle
- [State records](states.md) — solving state records from another simulation
- [GPU acceleration](gpu.md) — running on CUDA instead of the CPU accelerator
- [Command line](cli.md) — the `apthermo rocket` command over JSON documents
- [Troubleshooting](troubleshooting.md) — statuses and refusals in full
- [API.md](../../API.md) — the tree's public contract
