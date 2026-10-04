# API.md — Equilibrium.TraceGas

Namespace `APThermo.Equilibrium.TraceGas`. Every type is `internal`. The audience is
`src/Equilibrium`'s own file (`EquilibriumSolver`) and the tests node; the node is neither on the
package surface nor in the parent's tree contract. The parent's types the signatures name are
described in [the parent's API.md](../API.md).

## The pass ✅

```csharp
namespace APThermo.Equilibrium.TraceGas;

internal static class TraceGasPass
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static CaseStatus Run(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in EquilibriumResult result, double logPressure, ref IterationState state);
        // one trace-gas pass from the entry CaseSetup.Begin prepared (the failed iterate or a final's seed),
        // anchor in scratch.Tie.Elements.Multipliers; Ok leaves what a converged ConvergenceSequence leaves;
        // any other status restores result.Moles and result.Multipliers
}
```

## What the tests node drives ✅

```csharp
internal readonly struct TraceGasFrame(SystemLayout layout, double logPressure, double n, double sum, double temperature)
{
    public readonly SystemLayout Layout; public readonly double LogPressure; public readonly double N;
    public readonly double Sum; public readonly double Temperature;
    public static SystemLayout LayoutFor(in SpeciesTableView table, in EquilibriumProblem problem, int condensedCount);
}

internal static class TraceGasSystem
{
    public static bool Solve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, in TraceGasFrame frame);
    public static void Assemble(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, in TraceGasFrame frame);
        // the matrix does not depend on frame.N
}

internal static class TraceGasStep
{
    public static double Fractions(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double n, double logPressure);
    public static double LogFraction(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure, int j);
    public static double ControlFactor(in SpeciesTableView table, in EquilibriumScratch scratch, in SystemLayout layout);
    public static double Worst(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in TraceGasFrame frame);
    public static void Apply(in EquilibriumScratch scratch, in EquilibriumResult result, in SystemLayout layout, double lambda, ref double n);
    public static bool MoveTemperature(ref IterationState state, double lambda, double tau);
    public static bool Balanced(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result);
}

internal static class TraceGasIteration
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static CaseStatus Converge(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure, ref IterationState state);
}

internal static class TraceGasStart
{
    public static void SaveEntry(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result);
    public static void RestoreEntry(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state, double entryLogN, double entryTemperature);
    public static void Project(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in SystemLayout layout, double logPressure, double logN);
}

internal static class PhaseOneSeed
{
    public static bool Fetch(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, out double residual);
    public static void LoadPoint(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state, bool dropLevel);
    public static double Place(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, in TraceGasFrame frame, bool leastSquares);
}
```

## Errors

None thrown. `Run` returns `Ok`, `NotConverged`, `SingularMatrix` or `TemperatureOutOfRange`; the
`Recovery` node decides what the case reports.

## Side effects

`Run` writes `result.Moles`, `result.Multipliers`, the matrix scratch, `Corrections`, `LogMoles`,
`CondensedInSolution`, `Tie.LogMoles`, `Tie.CondensedMoles` and `Tie.CondensedSet`.

## Out of scope

- Which pass runs and when: `Recovery`.
- Whether a gas is required at all: `GasPhase`.
- The derivatives of a converged state: `StateRecord`.
