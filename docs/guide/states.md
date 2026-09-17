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

<!-- snippet: StatesSolve -->
```csharp
internal sealed class StatesSolve
{
    internal static void Run(TextWriter output)
    {
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        ElementalMixture mixture = solver.MixtureOf(propellant);

        double[] pressures = [1.0e5, 7.0e6, 20.0e6];
        var records = pressures.Select(p => new StateRecord(
            Pressure: p,
            Composition: mixture.ElementMoles,
            Temperature: 3000.0
        )).ToList();

        IReadOnlyList<EquilibriumResult> results = solver.SolveStates(records);

        output.WriteLine("LOX/LH2 O/F=6.0  tp states at T=3000 K");
        foreach (EquilibriumResult result in results)
        {
            var s = result.State.State;
            double P_MPa = result.Problem.Pressure / 1e6;
            output.WriteLine($"  P={P_MPa:F2} MPa   T={s.Temperature:F1} K   h={s.Enthalpy / 1e3:F1} kJ/kg   rho={s.Density:F3} kg/m³");
        }
    }
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
