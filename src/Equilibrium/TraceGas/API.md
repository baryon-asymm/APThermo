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
    public static SystemLayout TpLayoutFor(in SpeciesTableView table, int condensedCount);
    public static TraceGasFrame AtStart(in SpeciesTableView table, in EquilibriumProblem problem, int condensedCount, double logN, double temperature);
        // the frame a start is placed in: ln(p/p°) of the problem, n = exp(logN), S = 1 (2026-10-05)
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
    public static bool Stationary(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure);
        // the close guard: every gas with moles above zero within 1e-9 of its stationarity; a state without gas holds
    public static void Apply(in EquilibriumScratch scratch, in EquilibriumResult result, in SystemLayout layout, double lambda, ref double n);
    public static bool MoveTemperature(ref IterationState state, double lambda, double tau);
    public static double NextTemperature(double temperature, double lambda, double tau);
    public static bool Balanced(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result);
        // every active element within 3e-14 · b_i
}

internal static class TraceGasReport
{
    public static int KeepBalanceCarriers(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, double logN);
        // zeroes the gases below the second retention stage that carry no part of a balance (atoms within 1e-16 of
        // b_i for every active element); returns how many stay: IterationState.TraceCarriers
}

internal static class DataJunction
{
    public static bool Pins(in SpeciesTableView table, in EquilibriumScratch scratch, ref IterationState state, ref JunctionPin pin, double next, double tau);
        // the step to `next` crosses, again, the interval bound the previous crossing crossed, |tau| <= 1e-7: the convergence is pinned
    public static bool Decides(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state, ref JunctionPin pin);
        // after a converged tp convergence of a pin: true when it is the answer, false when the temperature moved for another
    public static double Bound(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, double t1, double t2, out double upper);
        // the largest temperature T_J on the lower interval of every species in play between t1 and t2, upper the next double; 0 when none crosses
}

internal struct JunctionPin
{
    public const int Free = 0; public const int MeasuringLower = 1; public const int AtUpper = 2; public const int AtLower = 3;
    public double LastBound; public double Lower; public double Upper; public double LowerMiss; public int Phase;
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
    public static void KeepRoomForTheGas(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state);
        // a set with as many records as active elements drops the record with the smallest positive amount (moles to zero, SetChanges counted);
        // a record at zero stays; called at the end of LoadPoint and after every change of the condensed set
    public static double Place(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, in TraceGasFrame frame, bool leastSquares);
    public static bool Point(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, bool withGas, out double residual);
        // the one call site of GasPhaseVerdict.PhaseOnePoint; Tie.LogMoles (the entry's moles) put aside and back (2026-10-05)
}

internal static class GasBasisSeed
{
    public static bool Place(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state);
        // start 5 (2026-10-05): the condensed basics as the set, π from the basis, ln n of the basic gas; with no gas basic and
        // ln S above zero at the basis's π, the gas mixture entered by the ratio test (2026-10-05); false: the start is skipped
}
```

## Errors

None thrown. `Run` returns `Ok`, `NotConverged`, `SingularMatrix` or `TemperatureOutOfRange`; the
`Recovery` node decides what the case reports.

## Side effects

`Run` writes `result.Moles`, `result.Multipliers`, the matrix scratch, `Corrections`, `LogMoles`,
`CondensedInSolution`, `Tie.LogMoles`, `Tie.CondensedMoles` and `Tie.CondensedSet`. `Stationary` writes nothing.

## Out of scope

- Which pass runs and when: `Recovery`.
- Whether a gas is required at all: `GasPhase`.
- The derivatives of a converged state: `StateRecord`.
