# States records

## Purpose

Solve a batch of state records: each record names a pressure, an elemental composition (mol/kg) and exactly one thermodynamic target (temperature, enthalpy or entropy). The solver returns the full equilibrium state for each record. This is the entry point for states produced by another simulation that needs APThermo's properties.

## When to use

- You have a set of (pressure, composition, target) tuples from another code and need the full thermodynamic state at each.
- You want to batch-solve many states in one solver call for efficiency.
- You are interfacing with a CFD or 1-D flow solver that produces local state records.

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build the propellant and obtain its elemental mixture (`MixtureOf`).
3. Build a list of `StateRecord` instances: each has a pressure, the element moles per kilogram, and exactly one of temperature, enthalpy or entropy.
4. Create a solver and call `SolveStates`.

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
    ["H"] = 141.73179242528607, ["O"] = 53.57343757533765,
};   // mol/kg, LOX/LH2 at O/F=6.0

var mixture = ElementalMixture.Create(elementMoles);
output.WriteLine($"mass = {solver.MassOf(mixture):F4} kg");

double[] pressures = [1.0e6, 7.0e6];   // Pa
var records = pressures.Select(pressure => new StateRecord(
    Pressure: pressure,
    Composition: elementMoles,
    Temperature: 3000.0)).ToList();     // K: a tp record

IReadOnlyList<EquilibriumResult> results = solver.SolveStates(records);

for (var i = 0; i < pressures.Length; i++)
{
    EquilibriumResult result = results[i];
    if (result.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  P={pressures[i] / 1e6:F2} MPa  FAILED: {result.Status}");
        continue;
    }

    var state = result.State.State;
    output.WriteLine($"  P={pressures[i] / 1e6:F2} MPa   T={state.Temperature:F1} K   h={state.Enthalpy / 1e3:F1} kJ/kg");
}
```

## Errors

- **Composition does not weigh one kilogram**: the element moles in a record must sum to one kilogram with the database's atomic weights (within 1 % by default); a record in mol/g or kmol/kg is refused naming the mass found.
- **More than one target given**: a record must have exactly one of `temperature`, `enthalpy` or `entropy`; giving two is an error at construction time.
- **A record fails numerically**: the batch still returns all results; the failed record carries a status and no state data.

## See also

- [Equilibrium states](equilibrium.md) — a single equilibrium problem from a propellant
- [Rocket solving](rocket.md) — a full nozzle problem
- [Command line](cli.md) — `apthermo states` over JSON record files
