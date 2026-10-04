using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>The enthalpy and the entropy of a gasless state, computed from its moles and the species functions, the way the condensed minimum's figures are defined (no mixing and no pressure term).</summary>
internal static class GaslessTarget
{
    /// <summary>h in J/kg for hp, s in J/(kg·K) for sp, of the solution's moles at its state's temperature.</summary>
    public static double Of(HostSolution solution, ProblemKind kind)
    {
        var table = solution.Case.Table;
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var view = buffers.View;
        var temperature = solution.State.Temperature;
        var sum = 0.0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            sum += solution.Moles[j] * (kind == ProblemKind.AssignedEnthalpyPressure
                ? SpeciesFunctions.HOverRT(view, j, temperature) * temperature
                : SpeciesFunctions.SOverR(view, j, temperature));
        }

        return sum * PhysicalConstants.R;
    }
}
