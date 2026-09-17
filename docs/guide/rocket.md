# Rocket solving

## Purpose

Solve a rocket nozzle problem with APThermo: given a propellant and a set of area ratios, compute the station-by-station thermodynamic state (temperature, pressure, density, enthalpy, entropy, molar mass) and performance figures (specific impulse, thrust coefficient, characteristic velocity) for the chamber, throat and each nozzle exit.

## When to use

- You need station-by-station results for a given propellant and nozzle geometry.
- You want to compare different area ratios or chamber pressures for the same propellant.
- You need transport properties (viscosity, thermal conductivity, Prandtl numbers) at each station.

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build a propellant from its oxidizer and fuel reactants with an oxidizer-to-fuel ratio.
3. Define the rocket problem: chamber pressure (Pa), area ratios, flow model, and whether to compute transport properties.
4. Create a solver bound to the CPU accelerator and call `Solve`.

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
if (throat.Status == CaseStatus.Ok && throat.Transport is { } transport)
{
    output.WriteLine($"  throat viscosity = {transport.Viscosity:E3} Pa·s");
}

foreach (var (species, fraction) in throat.MoleFractions.OrderByDescending(kv => kv.Value).Take(3))
{
    output.WriteLine($"  {species,-6} x={fraction:F4}");
}
```

## Errors

- **Invalid O/F ratio**: an oxidizer-to-fuel ratio that produces a non-physical mixture (negative enthalpy of formation beyond the database range) will cause the equilibrium solver to fail; the result carries a status rather than a partially filled state.
- **Missing species in database**: naming a reactant not present in the bundled database (e.g. a typo in the species name) raises an error at propellant build time, before any solve is attempted.
- **Temperature out of range**: a reactant temperature outside the polynomial intervals of its database record is refused with the record's valid range named.

## See also

- [Batch solving](batch.md) — solving multiple problems in one call
- [Equilibrium states](equilibrium.md) — a single equilibrium state without a nozzle
- [States records](states.md) — solving state records from another simulation
- [Command line](cli.md) — the `apthermo` tool over JSON documents
- [API.md](../../API.md) — the tree's public contract
