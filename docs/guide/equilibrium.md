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

<!-- snippet: EquilibriumSolve -->
```csharp
internal sealed class EquilibriumSolve
{
    internal static void Run(TextWriter output)
    {
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();

        var problem = new EquilibriumProblem { Pressure = 7.0e6 };

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        EquilibriumResult result = solver.Solve(propellant, problem);

        var state = result.State;
        output.WriteLine($"LOX/LH2 O/F=6.0  hp at P={result.Problem.Pressure / 1e6:F1} MPa");
        output.WriteLine($"  T       = {state.State.Temperature:F1} K");
        output.WriteLine($"  P       = {state.State.Pressure / 1e6:F3} MPa");
        output.WriteLine($"  h       = {state.State.Enthalpy / 1e3:F1} kJ/kg");
        output.WriteLine($"  rho     = {state.State.Density:F3} kg/m³");
        output.WriteLine($"  M       = {state.State.MolarMass:F2} kg/kmol");
    }
}
```

## Errors

- **No solution at the given pressure**: for some propellants and pressures the equilibrium solver may not converge; the result carries a failure status.
- **Temperature out of database range**: assigning a temperature beyond the polynomial intervals of any species in the mixture is refused at solve time.

## See also

- [Rocket solving](rocket.md) — a full nozzle problem with performance figures
- [Batch solving](batch.md) — multiple problems in one call
- [States records](states.md) — state records from another simulation
- [Command line](cli.md) — the `apthermo equilibrium` command
