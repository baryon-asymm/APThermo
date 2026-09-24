# State records

## Purpose

Solve a batch of state records: each record names a pressure, an elemental
composition (mol/kg) and exactly one thermodynamic target (temperature, enthalpy or
entropy). A record may also name exits (area ratios or pressure ratios), turning it
into a rocket case. This is the entry point for states produced by another simulation
that needs APThermo's properties, without going through a `Propellant`.

## When to use

- You have a set of (pressure, composition, target) tuples from another code and need
  the full thermodynamic state at each.
- Some of those states also need a nozzle (exits): a CFD or 1-D flow solver handing
  you a chamber state to expand.
- You want to batch-solve many states in one solver call for efficiency.

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build a list of `StateRecord` instances: each has a pressure (Pa), the element
   moles per kilogram of mixture (mol/kg), and exactly one of `Temperature` (K),
   `Enthalpy` (J/kg) or `Entropy` (J/(kg·K)). The element moles must weigh one
   kilogram with the database's atomic weights, within the tolerance in force.
3. Create a solver and call `SolveStates` for records without exits, or
   `SolveRocketStates` for records with `AreaRatios` or `PressureRatios` set (those
   need `Enthalpy`, since a rocket case needs an enthalpy to search the chamber
   from).

<!-- snippet: StatesSolveUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
```

`output` is any `TextWriter` (`Console.Out` in a console application):

<!-- snippet: StatesSolve -->
```csharp
var database = SpeciesDatabase.LoadBundled();
using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

var elementMoles = new Dictionary<string, double>
{
    ["H"] = 141.73179242528607,
    ["O"] = 53.57343757533765,
};   // mol/kg, LOX/LH2 at O/F=6.0

var mixture = ElementalMixture.Create(elementMoles);
output.WriteLine($"mass = {solver.MassOf(mixture):F4} kg");

double[] pressures = [1.0e6, 7.0e6];   // Pa
var records = pressures.Select(pressure => new StateRecord(
    Pressure: pressure,
    Composition: elementMoles,
    Temperature: 3000.0)).ToList();     // K: a tp record

var results = solver.SolveStates(records);

for (var i = 0; i < pressures.Length; i++)
{
    var result = results[i];
    if (result.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  P={pressures[i] / 1e6:F2} MPa  FAILED: {result.Status}");
        continue;
    }

    var state = result.State.State;
    output.WriteLine($"  P={pressures[i] / 1e6:F2} MPa   T={state.Temperature:F1} K   h={state.Enthalpy / 1e3:F1} kJ/kg");
}
```

### A record with exits

Setting `AreaRatios` or `PressureRatios` on a record (`HasExits`) turns it into a
rocket case, solved through `SolveRocketStates`; the result is a `RocketResult` with
a station per exit, exactly as [Rocket solving](rocket.md) describes:

<!-- snippet: RocketStatesUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
```

<!-- snippet: RocketStates -->
```csharp
var database = SpeciesDatabase.LoadBundled();
using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

var composition = new Dictionary<string, double>
{
    ["C"] = 9.505849129331365,
    ["H"] = 35.214695099119155,
    ["O"] = 15.704786718374072,
    ["N"] = 6.007718569653603,
    ["Cl"] = 3.3293409533388547,
    ["Al"] = 14.709996403305084,
};   // mol/kg

var record = new StateRecord(Pressure: 6.5e6, Composition: composition, Enthalpy: -1527829.408385985)
{
    AreaRatios = [8.0, 12.0],
};

var results = solver.SolveRocketStates([record]);
var result = results[0];

output.WriteLine($"state record with exits  status={result.Status}  mass={result.MixtureMass:F4} kg");
foreach (var station in result.Stations)
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

- **Composition does not weigh one kilogram**: the element moles in a record must sum
  to one kilogram with the database's atomic weights, within the tolerance in force
  (1 % by default); `SolveStates`/`SolveRocketStates` throw `MixtureMassException`
  naming the record's index, the mass found and the tolerance.
- **A record breaks a rule of its own shape**: none or more than one of
  `Temperature`, `Enthalpy` and `Entropy`; exits without an `Enthalpy`; a `Flow`
  named on a record without exits; a record with exits given to `SolveStates`, or
  one without exits given to `SolveRocketStates`. The `StateRecord` constructor
  itself validates nothing — these rules are checked when the batch is solved, and
  raise `StateRecordException` naming the record's index and the reason.
- **A record fails numerically**: the batch still returns every result; the failed
  record's station(s) carry a status other than `Ok` and a zero state, never no
  station at all.

## See also

- [Equilibrium states](equilibrium.md) — a single equilibrium problem from a propellant
- [Rocket solving](rocket.md) — a full nozzle problem from a propellant
- [Command line](cli.md) — `apthermo states` over JSON record files
- [Troubleshooting](troubleshooting.md) — mass-tolerance and shape refusals in full
