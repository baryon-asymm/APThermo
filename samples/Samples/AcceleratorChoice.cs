// snippet-start: AcceleratorChoiceUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
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

        var cpuTemperature = cpuSolver.Solve(propellant, problem).Stations[1].State.Temperature;
        var autoTemperature = autoSolver.Solve(propellant, problem).Stations[1].State.Temperature;
        var agrees = Math.Abs(cpuTemperature - autoTemperature) <= 1.0e-4 * cpuTemperature;

        output.WriteLine($"the Auto-chosen accelerator agrees with the CPU accelerator on the throat temperature: {agrees}");
        // snippet-end
    }
}
