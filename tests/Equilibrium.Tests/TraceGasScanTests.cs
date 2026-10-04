using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The scan fact (TraceGas BOOT.md, <c>## Acceptance criteria</c>): the tp states of the design's scan families
/// (<see cref="TraceGasCases.ScanFamilies"/>: the binary scan, the scan of 17 systems, and the families of the node's other facts) against the
/// statuses of the code before the pass, the close guard and the trace-gas finals, recorded once in <c>TraceGasScanBaseline.txt</c>.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceGasScanTests
{
    /// <summary>The path of the baseline: <c>name</c> and <c>status</c> of every state, tab-separated, <c>#</c> lines being comments.</summary>
    private static string BaselinePath => RepositoryPaths.Resolve("tests", "Equilibrium.Tests", "TraceGasScanBaseline.txt");

    /// <summary>
    /// Every state that ends <c>Ok</c> is clear of the equilibrium conditions at 1e-9 (every reported gas on its stationarity included);
    /// the states that end <c>NoGasPhase</c> are the verdict's (<c>ResidueVerdictTests</c> checks the residue set against the gasless
    /// conditions); a state ends <c>NotConverged</c> only as declared in <c>TraceGasLeftovers.txt</c>; no state that ended <c>Ok</c> before ends otherwise; the declarations are exactly the states that end
    /// <c>NotConverged</c>, so a state the pass later settles makes the list stale.
    /// </summary>
    [Fact]
    [Trait("Category", "LongRunning")]
    public void EveryStateOfTheScanFamiliesEndsClearAndNoOkIsLostAndOnlyTheDeclaredLeftoversRemain()
    {
        var baseline = File.ReadAllLines(BaselinePath)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => parts[0], parts => Enum.Parse<CaseStatus>(parts[1]));
        var problems = new List<string>();
        var notConverged = new HashSet<string>();
        var seen = new HashSet<string>();
        foreach (var state in TraceGasCases.ScanFamilies())
        {
            if (!seen.Add(state.Name))
            {
                continue;
            }

            var solution = state.Solve();
            if (!baseline.TryGetValue(state.Name, out var before))
            {
                problems.Add($"{state.Name}: not in the baseline");
            }
            else if (before == CaseStatus.Ok && solution.Status != CaseStatus.Ok)
            {
                problems.Add($"{state.Name}: Ok before, {solution.Status} now");
            }

            if (solution.Status == CaseStatus.NotConverged)
            {
                _ = notConverged.Add(state.Name);
            }
            else
            {
                problems.AddRange(Violations(state, solution));
            }
        }

        Assert.True(seen.Count > 0 && seen.Count == baseline.Count, $"{seen.Count} states against {baseline.Count} in the baseline");
        var declared = TraceGasLeftovers.NotConverged.Where(seen.Contains).ToHashSet();
        problems.AddRange(notConverged.Except(declared).Select(name => $"{name}: NotConverged and not declared"));
        problems.AddRange(declared.Except(notConverged).Select(name => $"{name}: declared NotConverged but not"));
        Assert.True(problems.Count == 0, $"{problems.Count} problems in {seen.Count} states:\n" + string.Join("\n", problems.Take(30)));
    }

    /// <summary>
    /// The trace-excess scan (<see cref="TraceGasCases.TraceScan"/>: the 17 systems at ± 1e-8 and ± 1e-10 of their last element, 1 632 tp states
    /// the design's scans did not walk): every state ends <c>Ok</c> clear of the conditions at 1e-9 with every element within the relative
    /// invariant, or <c>NoGasPhase</c>, or <c>NotConverged</c> exactly as declared in <c>TraceGasLeftovers.txt</c>; and every tp state the file
    /// declares <c>NotConverged</c> belongs to this scan or to <see cref="TraceGasCases.ScanFamilies"/>. Red without room for the gas:
    /// 50 states end <c>NotConverged</c> and 42 of them are not declared.
    /// </summary>
    [Fact]
    [Trait("Category", "LongRunning")]
    public void EveryStateOfTheTraceExcessScanEndsClearOrIsDeclared()
    {
        var problems = new List<string>();
        var notConverged = new HashSet<string>();
        var seen = new HashSet<string>();
        foreach (var state in TraceGasCases.TraceScan())
        {
            _ = seen.Add(state.Name);
            var solution = state.Solve();
            if (solution.Status == CaseStatus.NotConverged)
            {
                _ = notConverged.Add(state.Name);
            }
            else if (solution.Status is not (CaseStatus.Ok or CaseStatus.NoGasPhase))
            {
                problems.Add($"{state.Name}: {solution.Status}");
            }
            else
            {
                problems.AddRange(Violations(state, solution));
            }
        }

        var declared = TraceGasLeftovers.NotConverged.Where(seen.Contains).ToHashSet();
        problems.AddRange(notConverged.Except(declared).Select(name => $"{name}: NotConverged and not declared"));
        problems.AddRange(declared.Except(notConverged).Select(name => $"{name}: declared NotConverged but not"));
        var known = TraceGasCases.ScanFamilies().Select(state => state.Name).ToHashSet();
        problems.AddRange(TraceGasLeftovers.NotConverged.Where(name => !seen.Contains(name) && !known.Contains(name)).Select(name => $"{name}: declared, in no scan"));
        Assert.True(seen.Count == 1632, $"{seen.Count} states");
        Assert.True(problems.Count == 0, $"{problems.Count} problems in {seen.Count} states:\n" + string.Join("\n", problems.Take(60)));
    }

    private static IEnumerable<string> Violations(TraceGasCase state, HostSolution solution)
    {
        if (solution.Status != CaseStatus.Ok)
        {
            return [];
        }

        var violations = EquilibriumConditions.Violations(solution, Tolerances.EveryGasChemicalPotential);
        violations.AddRange(EquilibriumConditions.EveryGasViolations(solution, Tolerances.EveryGasChemicalPotential));
        return violations.Select(violation => $"{state.Name}: {violation}");
    }
}
