# API.md — Equilibrium.GasPhase

Namespace `APThermo.Equilibrium.GasPhase`. Internal to `src/Equilibrium`'s assembly: used by the
`Recovery` node and by the tests node; neither on the package surface nor in the parent's tree contract.

## Verdict ✅

```csharp
internal enum GasVerdict { Undecided = 0, GasRequired = 1, Gasless = 2 }
internal struct CondensedFigures { public double Enthalpy; public double Entropy; public double HeatCapacity; }  // J/kg, J/(kg·K), J/(kg·K)
internal static class GasPhaseVerdict
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static GasVerdict Decide(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, out CondensedFigures figures);
        // problem: tp, its temperature the verdict's. Gasless: Moles = the condensed minimum, Multipliers = the certificate.
        // Otherwise Moles and Multipliers as on entry; figures default.
    public static double LogTangentSum(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure);
}
```

## Internals the tests node reads ✅

```csharp
internal static class CondensedSimplex
{
    public static int Minimize(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, double temperature, out GasVerdict stop);
        // the number of rows with the optimal basis in the scratch, or 0 with the verdict that ends the test
    public static void WriteMoles(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int m);
}
internal static class TangentPlane
{
    public static int Directions(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, int m);
        // the dimension of the face of optimal multipliers, or −1 when a solve failed
    public static double LogTangentSum(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure);
}
```

`Face` and `CoordinateRange` are the carriers of the search, internal to this node.

## Trace-gas seed ✅ (2026-10-04)

```csharp
internal static class GasPhaseVerdict
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PhaseOnePoint(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                     in EquilibriumResult result, bool withGas, out double residual);
        // tp at problem.Temperature. True when the program completes: result.Moles holds its point, every other species
        // zero — the phase-one vertex when the columns cannot hold every element (residual: the moles they cannot hold,
        // > 0), else the minimum over the columns (residual 0). The columns are the records and, withGas, every gas at
        // unit fraction (2026-10-05); the optimal basis then stays in CondensedInSolution, Corrections and
        // Tie.CondensedSet. False, nothing written, when phase one does not complete. One call site, TraceGas's
        // PhaseOneSeed.Point; it borrows the verdict's scratch and neither keeps nor restores the failed attempt's
        // moles and multipliers.
}

internal static class CondensedSimplex
{
    public static bool Point(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, in SimplexColumns columns, out double residual);
        // what PhaseOnePoint writes, from the program Minimize runs; both share its two phases
}

internal readonly struct SimplexColumns(double temperature, double logPressure, bool withGas)
{
    public readonly double Temperature; public readonly double LogPressure; public readonly bool WithGas;
    public int FirstColumn(in SpeciesTableView table);                 // 0 with the gases, else table.GasCount
}
```

⚠ 2026-10-05: was `PhaseOnePoint(…, out double residual)` and `Point(…, double temperature, out double residual)`,
now with `bool withGas` and `in SimplexColumns columns`: the trace-gas pass asks the program with the gases.

After any verdict but `Gasless`, `Decide` leaves the multipliers it restored in
`scratch.Tie.Elements.Multipliers` (2026-10-04): the trace-gas pass takes its anchor there.
