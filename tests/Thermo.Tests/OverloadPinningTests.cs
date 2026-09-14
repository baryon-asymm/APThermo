using System.Reflection;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;

namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>
/// Reaches the internal host-side H°/RT overload the table builder's join-and-cut uses (<c>SpeciesFunctions.HOverRT(TemperatureInterval, double)</c>,
/// BOOT.md's declared deviation) by reflection, without widening the node's public surface.
/// </summary>
internal static class HostEnthalpyAccessor
{
    private static readonly MethodInfo Method = typeof(SpeciesFunctions).GetMethod(
        "HOverRT", BindingFlags.Static | BindingFlags.NonPublic, [typeof(TemperatureInterval), typeof(double)])!;

    public static double HOverRT(TemperatureInterval interval, double temperature) =>
        (double)Method.Invoke(null, [interval, temperature])!;
}

/// <summary>
/// L1: the declared deviation of BOOT.md pinned. The formula for H°/RT is written twice — once over the table view
/// for the kernels, once over a <see cref="TemperatureInterval"/> for the builder's join-and-cut test — sharing only
/// the per-term helper, because the builder needs no accelerator and ILGPU gives no view over a managed array
/// outside a kernel. The two must agree bit for bit over every species and temperature of the thermo fixtures, so a
/// drift cannot hide below the join-and-cut's latent-heat threshold.
/// </summary>
public sealed class OverloadPinningTests : IClassFixture<CpuFixture>
{
    private readonly CpuFixture _cpu;

    public OverloadPinningTests(CpuFixture cpu) => _cpu = cpu;

    public static TheoryData<string> ThermoFixtureSpecies()
    {
        var data = new TheoryData<string>();
        foreach (var fixture in CeaFixtures.LoadAll("thermo"))
        {
            data.Add(fixture.Inputs.GetProperty("species").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ThermoFixtureSpecies))]
    public void Host_and_kernel_enthalpy_sums_give_the_same_bits(string name)
    {
        using var buffers = _cpu.Upload(name);
        var table = buffers.Table;
        var view = buffers.View;
        var intervals = RecordIntervals(name);

        var compared = 0;
        for (var k = 0; k < intervals.Count; k++)
        {
            var interval = intervals[k];
            // Every interval's own upper bound and midpoint are unambiguous (the interval rule gives a shared bound
            // to the lower interval); the lower bound is tested only for the very first interval, where it is the
            // piece's own RecordLow and nothing precedes it to share it with.
            foreach (var temperature in k == 0 ? [interval.TLow, Midpoint(interval), interval.THigh] : new[] { Midpoint(interval), interval.THigh })
            {
                var piece = table.PieceOf(name, temperature);
                Assert.True(piece >= 0, $"{name} at {temperature} K: PieceOf found no piece");
                var host = HostEnthalpyAccessor.HOverRT(interval, temperature);
                var kernel = SpeciesFunctions.HOverRT(view, piece, temperature);
                Assert.Equal(BitConverter.DoubleToInt64Bits(host), BitConverter.DoubleToInt64Bits(kernel));
                compared++;
            }
        }

        Assert.True(compared > 0, $"{name}: no points compared");
    }

    /// <summary>Every interval of every record of the name, in file order: the same resolution CondensedAssembly makes, products before the database[name] fallback.</summary>
    private List<TemperatureInterval> RecordIntervals(string name)
    {
        var records = _cpu.Database.Records(name).Where(record => record.Section == SpeciesSection.Products).ToList();
        if (records.Count == 0)
        {
            records = [_cpu.Database[name]];
        }

        return records.SelectMany(record => record.Intervals).ToList();
    }

    private static double Midpoint(TemperatureInterval interval) => (interval.TLow + interval.THigh) / 2.0;
}
