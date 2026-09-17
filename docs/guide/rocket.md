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

<!-- snippet: RocketSolve -->
```csharp
internal sealed class RocketSolve
{
    internal static void Run(TextWriter output)
    {
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();

        var problem = new RocketProblem
        {
            ChamberPressure = 7.0e6,
            AreaRatios = [20.0, 77.5],
            Flow = FlowModel.ShiftingEquilibrium,
            Transport = true,
        };

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        RocketResult result = solver.Solve(propellant, problem);

        output.WriteLine($"LOX/LH2 O/F=6.0  Pc={result.Problem.ChamberPressure / 1e6:F1} MPa");
        foreach (Station station in result.Stations)
        {
            double T = station.State.Temperature;
            double P_MPa = station.State.Pressure / 1e6;
            double Isp = station.Performance?.SpecificImpulse ?? 0;
            output.WriteLine($"  {station.Name,-8}  T={T:F1} K   P={P_MPa:F3} MPa   Isp={Isp:F1} m/s");
        }
    }
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
