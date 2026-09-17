using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// One CUDA-against-CPU-accelerator comparison: the worst deviation seen so far per field, and how many stations compared so far
/// stopped after a different number of Newton steps on the two accelerators, both accumulated across however many calls the
/// caller makes against the tolerance table this was built from. <see cref="CudaTests"/> owns one per test.
/// </summary>
internal sealed class GpuCpuComparison(ToleranceTable tolerances)
{
    private readonly Dictionary<string, double> _worst = new(StringComparer.Ordinal);

    public int DifferentSteps { get; private set; }

    /// <summary>Every rocket station of one batch, cpu against gpu: station status, state, figures and mole fractions.</summary>
    public List<string> Rocket(RocketBatchResult cpu, RocketBatchResult gpu, RocketFamily family)
    {
        var mismatches = new List<string>();
        var stationCount = cpu.StationCount;
        for (var k = 0; k < cpu.Count; k++)
        {
            var label = k < family.Members.Count && cpu.Count == family.Members.Count ? family.Members[k] : $"case {k}";
            if (cpu.Status[k] != gpu.Status[k])
            {
                mismatches.Add($"{label}: status cpu {cpu.Status[k]}, cuda {gpu.Status[k]}");
                continue;
            }

            for (var s = 0; s < stationCount; s++)
            {
                var index = k * stationCount + s;
                if (cpu.StationStatus[index] != gpu.StationStatus[index])
                {
                    mismatches.Add($"{label} station {s}: status cpu {cpu.StationStatus[index]}, cuda {gpu.StationStatus[index]}");
                    continue;
                }

                if (cpu.StationStatus[index] != CaseStatus.Ok)
                {
                    continue;
                }

                var sameSteps = cpu.Iterations[index] == gpu.Iterations[index];
                CountSteps(sameSteps);

                var where = $"{label} station {s} ({cpu.Iterations[index]}/{gpu.Iterations[index]} steps)";
                mismatches.AddRange(GpuCpuTolerances.Compare(cpu.Stations[index], gpu.Stations[index], where, Record));
                mismatches.AddRange(GpuCpuTolerances.Compare(cpu.Figures[index], gpu.Figures[index], where, Record));
                mismatches.AddRange(Moles(cpu.Moles, gpu.Moles, index, family.Table, sameSteps, where));
            }
        }

        return mismatches;
    }

    /// <summary>Mole fractions of one case or station, relative to the total moles, within the tier of the mole-fraction tolerance above the floor.</summary>
    public IEnumerable<string> Moles(double[] cpuMoles, double[] gpuMoles, long index, SpeciesTable table, bool sameSteps, string label)
    {
        var speciesCount = table.SpeciesCount;
        var offset = index * speciesCount;
        var cpuTotal = 0.0;
        var gpuTotal = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            cpuTotal += cpuMoles[offset + j];
            gpuTotal += gpuMoles[offset + j];
        }

        var relative = GpuCpuTolerances.MoleFractionRelative(tolerances, sameSteps);
        var floor = GpuCpuTolerances.MoleFractionFloor(tolerances);
        for (var j = 0; j < speciesCount; j++)
        {
            var x = cpuMoles[offset + j] / cpuTotal;
            var y = gpuMoles[offset + j] / gpuTotal;
            if (x < floor && y < floor)
            {
                continue;
            }

            Record(sameSteps ? "moleFraction" : "moleFractionAfterDifferentSteps", Math.Abs(x - y) / Math.Max(x, y));
            if (!GpuCpuTolerances.Matches(relative, x, y))
            {
                yield return $"{label} x({table.Species[j]}): cpu {x:R}, cuda {y:R}";
            }
        }
    }

    /// <summary>Records one field's deviation if it is the worst seen so far for that field.</summary>
    public void Record(string field, double deviation)
    {
        if (!_worst.TryGetValue(field, out var current) || deviation > current)
        {
            _worst[field] = deviation;
        }
    }

    /// <summary>Counts a station or case whose two accelerators stopped after a different number of Newton steps.</summary>
    public void CountSteps(bool sameSteps)
    {
        if (!sameSteps)
        {
            DifferentSteps++;
        }
    }

    /// <summary>The worst deviation per field, largest first, for a failure message.</summary>
    public string Worst() => string.Join(", ", _worst.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} {kv.Value:E1}"));
}
