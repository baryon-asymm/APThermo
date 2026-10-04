using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// The point one trace-gas step is linearized at (BOOT.md, "The unknowns and rows"): the shape of the system, ln(p/p°), the
/// gaseous moles <c>n</c> the iterate carries, the sum <c>S = Σ_j x_j</c> of the exact gas fractions there and the temperature
/// the species functions stand at. One value per step, read through <c>in</c>, which keeps every method of the node within
/// six parameters.
/// </summary>
internal readonly struct TraceGasFrame(SystemLayout layout, double logPressure, double n, double sum, double temperature)
{
    /// <summary>The unknowns of the system: the elements, the condensed species of the solution, δ and, for hp and sp, τ.</summary>
    public readonly SystemLayout Layout = layout;

    /// <summary>ln(p/p°).</summary>
    public readonly double LogPressure = logPressure;

    /// <summary>The gaseous kmol per kilogram the iterate carries; the system does not depend on it (the rows are divided by it).</summary>
    public readonly double N = n;

    /// <summary>S = Σ_j x_j over every gas in play at the present multipliers; 1 at convergence.</summary>
    public readonly double Sum = sum;

    /// <summary>K.</summary>
    public readonly double Temperature = temperature;

    /// <summary>The shape of the system of a case with <paramref name="condensedCount"/> condensed species in the solution: the problem's kind, the table's elements, the row stride of the scratch.</summary>
    public static SystemLayout LayoutFor(in SpeciesTableView table, in EquilibriumProblem problem, int condensedCount) =>
        new(problem.Kind, table.ElementCount, condensedCount, ScratchLayout.MaxUnknowns(table.ElementCount));

    /// <summary>The shape of the tp system of the same case: no temperature unknown and no energy row, as an hp or sp convergence pinned at a data junction solves.</summary>
    public static SystemLayout TpLayoutFor(in SpeciesTableView table, int condensedCount) =>
        new(ProblemKind.AssignedTemperaturePressure, table.ElementCount, condensedCount, ScratchLayout.MaxUnknowns(table.ElementCount));
}
