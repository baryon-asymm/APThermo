// snippet-start: CustomPropellantUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>An aluminized composite propellant: two database reactants by mass fraction, one custom reactant, one omitted species.</summary>
internal sealed class CustomPropellant
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: CustomPropellant
        var database = SpeciesDatabase.LoadBundled();

        // IPDI-cured HTPB R-45M after Thomas and Petersen, AIAA Journal 2021, doi:10.2514/1.J060972.
        var binder = new CustomReactantDefinition(
            Formula: [new ElementCount("C", 213.8), new ElementCount("H", 323.0), new ElementCount("O", 4.6), new ElementCount("N", 2.3)],
            Enthalpy: 342000.0,       // J/mol of that formula unit at Temperature (+114 kJ/kg)
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
        var result = solver.Solve(propellant, problem);

        output.WriteLine($"AP/HTPB/Al  Pc={problem.ChamberPressure / 1e6:F1} MPa  status={result.Status}");
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
        // snippet-end
    }
}
