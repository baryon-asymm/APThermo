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
    public static void WriteMoles(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in EquilibriumProblem problem, int m);
}
internal static class TangentPlane
{
    public static int Directions(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, int m);
        // the dimension of the face of optimal multipliers, or −1 when a solve failed
    public static double LogTangentSum(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure);
}
```

`Face` and `CoordinateRange` are the carriers of the search, internal to this node. The trace-gas design adds
its phase-one entry here under ⏳.
