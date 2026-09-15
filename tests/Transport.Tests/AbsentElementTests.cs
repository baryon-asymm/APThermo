using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

/// <summary>
/// L1: a table that holds, besides the case's species in their order, the species of elements the case lacks changes nothing: every
/// figure of every station equals the case's own table bit for bit, because the set's thresholds count the gases of the case and the
/// sums run over the set in table order.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class AbsentElementTests(CpuFixture fixture)
{
    [Theory]
    [InlineData("lox-rp1_of2.6_pc10MPa_shiftingEquilibrium", "ap-htpb-al_pc7MPa_shiftingEquilibrium")]
    [InlineData("lox-lh2_of6_pc7MPa_shiftingEquilibrium", "nto-udmh_of2.2_pc1MPa_shiftingEquilibrium")]
    [InlineData("nto-udmh_of2.2_pc1MPa_shiftingEquilibrium", "ap-htpb-al_pc7MPa_shiftingEquilibrium")]
    public void A_table_with_the_species_of_absent_elements_gives_the_same_bits(string name, string other)
    {
        var c = TransportHost.LoadRocket(name);
        var o = TransportHost.LoadRocket(other);
        var (own, ownTransport) = TransportHost.TablesOf(fixture, c);
        var elements = TransportHost.ElementsOf(c).Concat(TransportHost.ElementsOf(o)).Distinct(StringComparer.Ordinal).ToArray();
        var products = TransportHost.ProductsOf(c).Concat(TransportHost.ProductsOf(o)).Distinct(StringComparer.Ordinal).ToArray();
        Assert.True(elements.Length > own.ElementCount, "the other case must bring an element the case lacks");
        var union = SpeciesTable.Build(fixture.Database, elements, products);
        var unionTransport = TransportTable.Build(fixture.Transport, union);
        Assert.True(union.GasCount > own.GasCount, "the other case must bring gaseous species");

        using var ownSpecies = SpeciesTableBuffers.Upload(fixture.Accelerator, own);
        using var ownBuffers = TransportTableBuffers.Upload(fixture.Accelerator, ownTransport);
        using var unionSpecies = SpeciesTableBuffers.Upload(fixture.Accelerator, union);
        using var unionBuffers = TransportTableBuffers.Upload(fixture.Accelerator, unionTransport);
        var stations = TransportHost.StationsWithTransport(c);
        Assert.NotEmpty(stations);
        var differences = new List<string>();
        foreach (var station in stations)
        {
            var label = station.GetProperty("station").GetString();
            var temperature = station.GetProperty("temperature").GetDouble();
            var expected = TransportHost.Evaluate(fixture.Accelerator, ownSpecies, ownBuffers, temperature, TransportHost.MolesOf(own, station));
            var actual = TransportHost.Evaluate(fixture.Accelerator, unionSpecies, unionBuffers, temperature, TransportHost.MolesOf(union, station));
            if (expected.Status != actual.Status)
            {
                differences.Add($"{label}: status {expected.Status} in the own table, {actual.Status} in the union");
                continue;
            }

            foreach (var field in typeof(TransportFigures).GetFields())
            {
                var p = field.GetValue(expected.Figures)!;
                var q = field.GetValue(actual.Figures)!;
                var same = p is double x ? Bits.Same(x, (double)q) : p.Equals(q);
                if (!same)
                {
                    differences.Add($"{label} {field.Name}: own table {p}, union {q}");
                }
            }
        }

        Assert.True(differences.Count == 0, $"{name} in the table with {other}:\n" + string.Join("\n", differences));
    }
}
