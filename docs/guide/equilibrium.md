# Equilibrium states

## Purpose

Compute a single chemical equilibrium state for a propellant at a given pressure: the temperature, density, enthalpy, entropy, molar mass and composition of the combustion products in thermodynamic equilibrium. The specification can be by temperature (tp), by enthalpy (hp) or by entropy (sp).

## When to use

- You need the equilibrium state at one pressure without a nozzle geometry.
- You are building a thermodynamic property table for a propellant.
- You want to find the temperature corresponding to a given enthalpy (hp problem) or the enthalpy corresponding to a given temperature (tp problem).

## Steps

1. Load the bundled NASA thermodynamic database.
2. Build the propellant (oxidizer, fuel, O/F ratio).
3. Define an `EquilibriumProblem` with the pressure and, for tp problems, the assigned temperature. For hp problems, omit the temperature: the solver uses the propellant's own enthalpy.
4. Create a solver and call `Solve`.

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

## Errors

- **No solution at the given pressure**: for some propellants and pressures the equilibrium solver may not converge; the result carries a failure status.
- **Temperature out of database range**: assigning a temperature beyond the polynomial intervals of any species in the mixture is refused at solve time.

## See also

- [Rocket solving](rocket.md) — a full nozzle problem with performance figures
- [Batch solving](batch.md) — multiple problems in one call
- [States records](states.md) — state records from another simulation
- [Command line](cli.md) — the `apthermo equilibrium` command
