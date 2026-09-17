// snippet-start: AcceleratorChoiceUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>Binds the accelerator through <c>AcceleratorProbe</c> and lets <c>Auto</c> choose; prints only facts that hold on every machine.</summary>
internal sealed class AcceleratorChoice
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: AcceleratorChoice
        var database = SpeciesDatabase.LoadBundled();

        var info = AcceleratorProbe.Describe(new EngineOptions { Accelerator = AcceleratorKind.Auto });
        var versionKnown = !string.IsNullOrEmpty(info.IlgpuVersion);
        output.WriteLine($"AcceleratorProbe.Describe answered without throwing, IlgpuVersion is not empty: {versionKnown}");

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)   // K
            .Fuel("H2(L)", temperature: 20.27)        // K
            .OxidizerToFuelRatio(6.0)
            .Build();
        var problem = new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] };   // Pa

        using var cpuSolver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var autoSolver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Auto });

        var cpuThroat = cpuSolver.Solve(propellant, problem).Stations[1];
        var autoThroat = autoSolver.Solve(propellant, problem).Stations[1];

        if (cpuThroat.Status == CaseStatus.Ok && autoThroat.Status == CaseStatus.Ok)
        {
            var agrees = Math.Abs(cpuThroat.State.Temperature - autoThroat.State.Temperature) <= 1.0e-4 * cpuThroat.State.Temperature;
            output.WriteLine($"the Auto-chosen accelerator agrees with the CPU accelerator on the throat temperature: {agrees}");
        }
        else
        {
            output.WriteLine($"the Auto-chosen accelerator agrees with the CPU accelerator on the throat temperature: skipped, throat status {cpuThroat.Status}/{autoThroat.Status}");
        }
        // snippet-end
    }
}
