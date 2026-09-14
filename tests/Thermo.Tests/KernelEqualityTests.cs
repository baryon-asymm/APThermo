using AerospacePropellantThermodynamics.Harness;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>L1: the species functions inside an ILGPU kernel on the CPU accelerator give the same bits as the host calls.</summary>
public sealed class KernelEqualityTests : IClassFixture<CpuFixture>
{
    private readonly CpuFixture _cpu;

    public KernelEqualityTests(CpuFixture cpu) => _cpu = cpu;

    private const int ValuesPerPoint = 6;

    private static void Evaluate(Index1D i, SpeciesTableView table, ArrayView<int> species, ArrayView<double> temperatures, ArrayView<double> results)
    {
        var j = species[i];
        var t = temperatures[i];
        results[i * ValuesPerPoint + 0] = SpeciesFunctions.CpOverR(table, j, t);
        results[i * ValuesPerPoint + 1] = SpeciesFunctions.HOverRT(table, j, t);
        results[i * ValuesPerPoint + 2] = SpeciesFunctions.SOverR(table, j, t);
        results[i * ValuesPerPoint + 3] = SpeciesFunctions.GOverRT(table, j, t);
        results[i * ValuesPerPoint + 4] = SpeciesFunctions.IntervalOf(table, j, t);
        results[i * ValuesPerPoint + 5] = SpeciesFunctions.IsInRange(table, j, t) ? 1.0 : 0.0;
    }

    [Fact]
    public void Kernel_and_host_give_the_same_bits()
    {
        string[] names = ["H2O", "CO2", "H2", "N2", "AL2O3(a)", "AL2O3(L)", "C(gr)", "W(cr)", "e-"];
        using var buffers = _cpu.Upload(names);
        var view = buffers.View;
        double[] temperatures = [150.0, 200.0, 298.15, 500.0, 1000.0, 1000.0001, 2327.0, 3000.0, 6000.0, 6000.5, 12000.0, 25000.0];

        var species = new List<int>();
        var points = new List<double>();
        for (var j = 0; j < names.Length; j++)
        {
            foreach (var t in temperatures)
            {
                species.Add(j);
                points.Add(t);
            }
        }

        using var speciesBuffer = _cpu.Accelerator.Allocate1D(species.ToArray());
        using var temperatureBuffer = _cpu.Accelerator.Allocate1D(points.ToArray());
        using var resultBuffer = _cpu.Accelerator.Allocate1D<double>(points.Count * ValuesPerPoint);
        var kernel = _cpu.Accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, ArrayView<int>, ArrayView<double>, ArrayView<double>>(Evaluate);
        kernel(points.Count, view, speciesBuffer.View, temperatureBuffer.View, resultBuffer.View);
        _cpu.Accelerator.Synchronize();
        var results = resultBuffer.GetAsArray1D();

        var compared = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = species[i];
            var t = points[i];
            double[] host =
            [
                SpeciesFunctions.CpOverR(view, j, t),
                SpeciesFunctions.HOverRT(view, j, t),
                SpeciesFunctions.SOverR(view, j, t),
                SpeciesFunctions.GOverRT(view, j, t),
                SpeciesFunctions.IntervalOf(view, j, t),
                SpeciesFunctions.IsInRange(view, j, t) ? 1.0 : 0.0,
            ];
            for (var k = 0; k < ValuesPerPoint; k++)
            {
                Assert.True(Bits.Same(host[k], results[i * ValuesPerPoint + k]),
                            $"{names[j]} at {t} K, value {k}: host {host[k]:R}, kernel {results[i * ValuesPerPoint + k]:R}");
                compared++;
            }
        }

        Assert.Equal(names.Length * temperatures.Length * ValuesPerPoint, compared);
    }
}
