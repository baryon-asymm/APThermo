# Equilibrium states

## Purpose

Compute a single chemical equilibrium state for a propellant at a given pressure: the
temperature, density, enthalpy, entropy, molar mass and composition of the combustion
products in thermodynamic equilibrium, without a nozzle. The state can be assigned by
temperature (tp), by enthalpy (hp) or by entropy (sp).

## When to use

- You need the equilibrium state at one pressure without a nozzle geometry.
- You are building a thermodynamic property table for a propellant.
- You want the temperature at a given enthalpy or entropy (hp, sp), or the enthalpy
  or entropy at a given temperature (tp).

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build the propellant (oxidizer, fuel, O/F ratio).
3. Define an `EquilibriumProblem` with the pressure and `Kind`
   (`APThermo.Equilibrium.ProblemKind`):
   - `AssignedEnthalpyPressure` (hp, the default): set `Enthalpy`, or omit it to use
     the propellant's own enthalpy.
   - `AssignedEntropyPressure` (sp): set `Entropy`.
   - `AssignedTemperaturePressure` (tp): set `Temperature`.
4. Create a solver and call `Solve`. Check `Status` before reading the state: a
   non-`Ok` result carries no state, never a partial one.

<!-- snippet: EquilibriumKindsUsings -->
```csharp
using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
```

`output` is any `TextWriter` (`Console.Out` in a console application):

<!-- snippet: EquilibriumKinds -->
```csharp
var database = SpeciesDatabase.LoadBundled();

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)   // K
    .Fuel("H2(L)", temperature: 20.27)        // K
    .OxidizerToFuelRatio(6.0)
    .Build();

using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

void Print(string kind, EquilibriumResult result)
{
    if (result.Status != CaseStatus.Ok)
    {
        output.WriteLine($"  {kind,-2}  FAILED: {result.Status}");
        return;
    }

    output.WriteLine($"  {kind,-2}  T={result.State.State.Temperature:F1} K");
}

var hp = solver.Solve(propellant, new EquilibriumProblem { Pressure = 7.0e6 });   // Kind defaults to hp
Print("hp", hp);

var sp = solver.Solve(propellant, new EquilibriumProblem
{
    Kind = ProblemKind.AssignedEntropyPressure,
    Pressure = 7.0e6,
    Entropy = hp.State.State.Entropy,
});
Print("sp", sp);

var tp = solver.Solve(propellant, new EquilibriumProblem
{
    Kind = ProblemKind.AssignedTemperaturePressure,
    Pressure = 7.0e6,
    Temperature = 3000.0,   // K
});
Print("tp", tp);
```

The sp case above assigns the entropy the hp solve found, so the two states coincide;
in general an sp problem is independent of any other solve.

## Errors

- **A non-positive pressure, or an hp problem on a mixture without an enthalpy**:
  `ArgumentException` at `Solve`, before any kernel runs.
- **The solver does not converge to a state at the given pressure and target**: the
  result's `Status` is `NotConverged` or `SingularMatrix`; no exception, and no
  partial state.
- **The hp or sp iterate leaves the database's temperature range [100 K, 20000 K]**:
  `Status` is `TemperatureOutOfRange`. This is a per-case status, not a refusal at
  solve time: `Solve` never throws for it.

## See also

- [Rocket solving](rocket.md) — a full nozzle problem with performance figures
- [Batch solving](batch.md) — multiple problems in one call
- [State records](states.md) — state records from another simulation
- [Command line](cli.md) — the `apthermo equilibrium` command
- [Troubleshooting](troubleshooting.md) — every status and its meaning
