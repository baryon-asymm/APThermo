using System.Reflection;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Problems;
using APThermo.Thermo;

namespace APThermo.Benchmarks;

/// Compares the engine path's `RocketBatchResult` with the consumer path's
/// `IReadOnlyList{RocketResult}` for the same batch of identical cases (BOOT.md,
/// Constraints, group 7), so that the packaging decision the root `BOOT.md` names
/// (`## Delivery`, Tree contracts: the engine leaves the package surface, `Solver`
/// stays the consumer's batch entry) is measured against a proven-identical result,
/// not an assumed one, on each accelerator. Every `MixtureState` and
/// `PerformanceFigures` field is checked for bit equality first (`Harness.Bits.Same`);
/// on the CPU accelerator a dry run found every field of every case bit-for-bit equal
/// (BOOT.md, group 7's acceptance criterion), so a difference there is a finding, not
/// an expected outcome. On CUDA the same dry run found `Solver` and the engine not
/// bit-for-bit identical even though both call the identical kernel on the identical
/// batch (worst observed 3.5e-12 relative on temperature): the same last-ULP pattern
/// the root `BOOT.md`'s GPU-equals-CPU invariant documents for CUDA against the CPU
/// accelerator, so a CUDA field is held to the GPU/CPU tolerance tiers this node's own
/// `BOOT.md` already quotes from the execution tests node's `GpuCpuTolerances.Entries`
/// (relative 1e-10 on temperature, 1e-9 on every other field); a mole fraction, the one
/// value `Solver` derives — `n_j` over the moles of all species (`Problems/API.md`) —
/// by a division whose rounding can differ from the engine's own summation even on the
/// CPU accelerator, is held on every accelerator to the fixtures node's tolerance table
/// for two paths of the tree's own code reaching the same mole fraction
/// (`moleFractionFloor`, `polishThresholdRelative`), the same relation the execution
/// tests node applies to CUDA against the CPU accelerator, applied here to the consumer
/// path against the engine.
internal sealed class EngineSolverComparison
{
    // The execution tests node's GPU/CPU tolerance tiers (`GpuCpuTolerances.Entries`),
    // already quoted in this node's own BOOT.md Invariants; Benchmarks may not name
    // that node's internal type (its API.md exposes nothing outward), so the two
    // figures are repeated here, as BOOT.md repeats them, rather than read from code.
    private const double TemperatureRelativeTolerance = 1.0e-10;
    private const double OtherFieldRelativeTolerance = 1.0e-9;

    private static readonly PropertyInfo[] StateFields = typeof(MixtureState).GetProperties();
    private static readonly PropertyInfo[] PerformanceFields = typeof(APThermo.Performance.PerformanceFigures).GetProperties();

    private readonly ToleranceTable _tolerances;
    private readonly List<string> _mismatches = [];
    private double _worstFieldRelative;
    private double _worstMoleFractionRelative;

    public EngineSolverComparison(ToleranceTable tolerances) => _tolerances = tolerances;

    public bool Passed => _mismatches.Count == 0;
    public bool ExactlyBitEqual => Passed && _worstFieldRelative == 0.0 && _worstMoleFractionRelative == 0.0;
    public double WorstFieldRelative => _worstFieldRelative;
    public double WorstMoleFractionRelative => _worstMoleFractionRelative;
    public IReadOnlyList<string> Mismatches => _mismatches;

    public void Compare(RocketBatchResult engine, IReadOnlyList<string> engineSpecies, IReadOnlyList<RocketResult> solver)
    {
        for (var i = 0; i < solver.Count; i++)
        {
            CompareCase(engine, engineSpecies, solver[i], i);
        }
    }

    private void CompareCase(RocketBatchResult engine, IReadOnlyList<string> species, RocketResult result, int caseIndex)
    {
        if (result.Status != engine.Status[caseIndex])
        {
            _mismatches.Add($"case {caseIndex}: status engine={engine.Status[caseIndex]} solver={result.Status}");
        }
        for (var s = 0; s < result.Stations.Count; s++)
        {
            CompareStation(engine, species, result.Stations[s], caseIndex, s);
        }
    }

    private void CompareStation(RocketBatchResult engine, IReadOnlyList<string> species, Station station, int caseIndex, int stationIndex)
    {
        var flat = caseIndex * engine.StationCount + stationIndex;
        if (station.Status != engine.StationStatus[flat])
        {
            _mismatches.Add($"case {caseIndex} station {stationIndex}: status engine={engine.StationStatus[flat]} solver={station.Status}");
        }
        CompareStructFields(StateFields, engine.Stations[flat], station.State, caseIndex, stationIndex, "state");
        CompareStructFields(PerformanceFields, engine.Figures[flat], station.Performance!.Value, caseIndex, stationIndex, "performance");
        CompareMoles(engine, species, station.MoleFractions, flat, caseIndex, stationIndex);
    }

    private void CompareStructFields<T>(PropertyInfo[] fields, T expected, T actual, int caseIndex, int stationIndex, string label)
        where T : struct
    {
        foreach (var field in fields)
        {
            if (field.PropertyType != typeof(double)) continue;
            var e = (double)field.GetValue(expected)!;
            var a = (double)field.GetValue(actual)!;
            if (Bits.Same(e, a)) continue;

            var relative = RelativeDifference(e, a);
            _worstFieldRelative = Math.Max(_worstFieldRelative, relative);
            var tolerance = field.Name == "Temperature" ? TemperatureRelativeTolerance : OtherFieldRelativeTolerance;
            if (relative > tolerance)
            {
                _mismatches.Add(
                    $"case {caseIndex} station {stationIndex}: {label}.{field.Name} engine={e:r} solver={a:r} relative={relative:e3}");
            }
        }
    }

    private void CompareMoles(RocketBatchResult engine, IReadOnlyList<string> species,
        IReadOnlyDictionary<string, double> solverFractions, int flat, int caseIndex, int stationIndex)
    {
        var start = flat * engine.SpeciesCount;
        var total = 0.0;
        for (var j = 0; j < engine.SpeciesCount; j++)
        {
            total += engine.Moles[start + j];
        }
        for (var j = 0; j < engine.SpeciesCount; j++)
        {
            var expected = total > 0 ? engine.Moles[start + j] / total : 0.0;
            CompareMoleFraction(species[j], expected, solverFractions, caseIndex, stationIndex);
        }
    }

    private void CompareMoleFraction(string name, double expected, IReadOnlyDictionary<string, double> solverFractions,
        int caseIndex, int stationIndex)
    {
        var actual = solverFractions.TryGetValue(name, out var value) ? value : double.NaN;
        if (Bits.Same(expected, actual)) return;
        if (expected < _tolerances.For("moleFractionFloor").Absolute) return;

        var relative = RelativeDifference(expected, actual);
        _worstMoleFractionRelative = Math.Max(_worstMoleFractionRelative, relative);
        if (relative > _tolerances.For("polishThresholdRelative").Relative)
        {
            _mismatches.Add(
                $"case {caseIndex} station {stationIndex}: moleFraction[{name}] engine={expected:e6} solver={actual:e6} relative={relative:e3}");
        }
    }

    // The largest-relative-difference formula of BOOT.md's CUDA comparison procedure
    // (`## Constraints`): `|a-b|/max(|a|,|b|)`.
    private static double RelativeDifference(double expected, double actual)
    {
        var scale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        return scale > 0 ? Math.Abs(expected - actual) / scale : 0.0;
    }
}
