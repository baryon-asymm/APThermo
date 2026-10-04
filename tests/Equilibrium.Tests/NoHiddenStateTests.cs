using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// No hidden state (Equilibrium BOOT.md, Invariants, "Deterministic and stateless"; the nondeterminism investigation of 2026-10-04): the same
/// batch of cases run with every buffer the solver owns or writes holding zeros, NaN and 1e300 gives the same result for every case, whatever
/// the buffers held: the status, the iterations, every mole and every multiplier bit for bit, and the state bit for bit for every case that
/// reports one. A case that ends otherwise than <c>Ok</c> or <c>NoGasPhase</c> writes no state (Equilibrium BOOT.md, "Failures are values"), so
/// its state is the buffer's and is not compared; the harness zeroes it (<see cref="HostSolver"/>).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class NoHiddenStateTests
{
    private static readonly string[] Kinds = ["tp", "hp", "sp"];
    private static readonly BufferFill[] Poisons = [BufferFill.NotANumber, BufferFill.Large];

    /// <summary>
    /// CaCO3 + 1e-7 O at 10 MPa and 300 K: a state that ends <c>NotConverged</c> after every start of the trace-gas pass, the gas basis
    /// included (TraceGas BOOT.md, "Declared leftovers": its balancing CaO lies below the rounding of the balance), so that the batch
    /// walks a failure through the whole pass now that the declared leftovers are settled.
    /// </summary>
    private const string FailingEveryStart = "CaCO3|1E-07|10000000|300";

    /// <summary>
    /// The batch: every fixture case of the three equilibrium kinds, the magnesite band (the trace-carrier states, 41), the states declared
    /// <c>NotConverged</c> in <c>TraceGasLeftovers.txt</c>, the nine states the gas basis settles (<see cref="GasBasisStartTests.Settled"/>), the
    /// one state that fails after every start of the pass (<see cref="FailingEveryStart"/>), the gasless states of KO2 − 1e-10 O, and the hp and sp states at the junction
    /// of the data warm-started from their tp composition: cases that end <c>Ok</c>, <c>NoGasPhase</c> and in every failure the solver reports.
    /// </summary>
    private static List<(string Label, EquilibriumCase Case, double[]? Estimate)> Batch()
    {
        var batch = new List<(string, EquilibriumCase, double[]?)>();
        foreach (var kind in Kinds)
        {
            batch.AddRange(HostSolver.CaseNames(kind).Select(name => ($"{kind}/{name}", HostSolver.Of(CpuFixture.Shared, HostSolver.Load(kind, name)), (double[]?)null)));
        }

        var known = TraceGasCases.ScanFamilies().Concat(TraceGasCases.TraceScan()).DistinctBy(state => state.Name).ToDictionary(state => state.Name);
        var states = TraceGasCases.MagnesiteBand().Concat(TraceGasLeftovers.NotConverged.Where(known.ContainsKey).Select(name => known[name]))
            .Concat(GasBasisStartTests.Settled.Select(name => known[name]))
            .Concat(TraceGasCases.Binary([-1.0e-10]).Where(state => state.Name.StartsWith("binary-ko2|", StringComparison.Ordinal)))
            .Concat(TraceGasCases.Scan([1.0e-7]).Where(state => state.Name == FailingEveryStart));
        batch.AddRange(states.Select(state => (state.Name, state.AsCase(), (double[]?)null)));
        foreach (var state in TraceGasCases.JunctionStates().Where(state => state.Pressure == 1.0e5))
        {
            var tp = state.Solve();
            batch.Add((state.Name + " tp", state.AsCase(), null));
            foreach (var kind in new[] { ProblemKind.AssignedEnthalpyPressure, ProblemKind.AssignedEntropyPressure })
            {
                batch.Add(($"{state.Name} {kind} warm", state.CaseAtStateOf(tp, kind) with { Temperature = state.Temperature }, tp.Moles));
            }
        }

        return batch;
    }

    /// <summary>
    /// Every case of the batch gives, with the buffers poisoned by NaN and by 1e300, the same status, iterations, moles, multipliers and (for
    /// <c>Ok</c> and <c>NoGasPhase</c>) state as with zeros, bit for bit. Red when a field of the scratch the solver reads before it writes
    /// is left to the buffer: with the estimate of the gaseous moles written by the cold branch alone, the cases that start without one differ.
    /// </summary>
    [Fact]
    public void TheSameBatchGivesTheSameResultsWhateverTheBuffersHeld()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var problems = new List<string>();
        var batch = Batch();
        var statuses = new HashSet<CaseStatus>();
        foreach (var (label, problem, estimate) in batch)
        {
            var reference = HostSolver.SolveFilled(accelerator, problem, estimate, BufferFill.Zero);
            _ = statuses.Add(reference.Status);
            foreach (var poison in Poisons)
            {
                problems.AddRange(Differences(label, poison, reference, HostSolver.SolveFilled(accelerator, problem, estimate, poison)));
            }
        }

        Assert.True(batch.Count > 300 && statuses.Contains(CaseStatus.Ok) && statuses.Contains(CaseStatus.NoGasPhase) && statuses.Contains(CaseStatus.NotConverged),
            $"{batch.Count} cases ending {string.Join(", ", statuses)}");
        Assert.True(problems.Count == 0, $"{problems.Count} differences in {batch.Count} cases:\n" + string.Join("\n", problems.Take(20)));
    }

    private static IEnumerable<string> Differences(string label, BufferFill poison, HostSolution reference, HostSolution poisoned)
    {
        var where = $"{label} with {poison} buffers";
        if (reference.Status != poisoned.Status || reference.Iterations != poisoned.Iterations)
        {
            yield return $"{where}: {reference.Status} after {reference.Iterations} against {poisoned.Status} after {poisoned.Iterations}";
            yield break;
        }

        for (var j = 0; j < reference.Moles.Length; j++)
        {
            if (!Bits.Same(reference.Moles[j], poisoned.Moles[j]))
            {
                yield return $"{where}: moles[{reference.Case.Table.Species[j]}] {reference.Moles[j]:R} against {poisoned.Moles[j]:R}";
            }
        }

        for (var i = 0; i < reference.Multipliers.Length; i++)
        {
            if (!Bits.Same(reference.Multipliers[i], poisoned.Multipliers[i]))
            {
                yield return $"{where}: multiplier {i} {reference.Multipliers[i]:R} against {poisoned.Multipliers[i]:R}";
            }
        }

        if (reference.Status is CaseStatus.Ok or CaseStatus.NoGasPhase)
        {
            foreach (var difference in Bits.Differences(reference.State, poisoned.State, where))
            {
                yield return difference;
            }
        }
    }
}
