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
        var declared = TraceGasLeftovers.NotConverged.Where(name => !name.Contains("-cold|", StringComparison.Ordinal) && !name.Contains("-warm", StringComparison.Ordinal)).ToHashSet();
        problems.AddRange(notConverged.Except(declared).Select(name => $"{name}: NotConverged and not declared"));
        problems.AddRange(declared.Except(notConverged).Select(name => $"{name}: declared NotConverged but not"));
        Assert.True(problems.Count == 0, $"{problems.Count} problems in {seen.Count} states:\n" + string.Join("\n", problems.Take(30)));
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
