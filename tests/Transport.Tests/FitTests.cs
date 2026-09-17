using System.Text.Json;
using APThermo.Data;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

/// <summary>L0: the table holds the fits of the file in SI and evaluates them as the independent Python evaluation does.</summary>
[Collection(CpuCollection.Name)]
public sealed class FitTests(CpuFixture fixture)
{
    public static IEnumerable<object[]> Cases() => TransportHost.FitCases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Fit_values_match_the_independent_evaluation(string name)
    {
        var c = TransportHost.LoadFit(name);
        var species = c.Inputs.GetProperty("species").GetString()!;
        var partner = c.Inputs.GetProperty("partner").ValueKind == JsonValueKind.Null ? null : c.Inputs.GetProperty("partner").GetString();
        var (table, transport) = TablesFor(species, partner);
        using var speciesBuffers = SpeciesTableBuffers.Upload(fixture.Accelerator, table);
        using var buffers = TransportTableBuffers.Upload(fixture.Accelerator, transport);
        var view = buffers.View;
        var index = table.IndexOf(species);
        var kinds = c.Inputs.GetProperty("fits").EnumerateArray().Select(f => f[0].GetString()!).ToList();
        var viscosityFits = kinds.Count(k => k == "V");
        var mismatches = new List<string>();

        foreach (var property in c.Outputs.EnumerateObject())
        {
            var (start, count) = Run(transport, property.Name, index, partner is null ? -1 : table.IndexOf(partner));
            foreach (var entry in property.Value.EnumerateArray())
            {
                var temperature = entry.GetProperty("temperature").GetDouble();
                var position = entry.GetProperty("fit").GetInt32();     // into the fixture's list of fits, viscosity first
                var expected = entry.GetProperty("value").GetDouble();
                Assert.Equal(property.Name == "viscosity" ? "V" : "C", kinds[position]);
                var fit = property.Name == "viscosity" ? position : position - viscosityFits;
                Assert.True(fit >= 0 && fit < count, $"{name} {property.Name}: fit {fit} beyond the {count} fits of the table");
                var actual = TransportSolver.FitValue(in view, start + fit, temperature);
                if (!fixture.Tolerances.Matches("transportFit", expected, actual))
                {
                    mismatches.Add($"{property.Name} fit {fit} at {temperature} K: reference {expected:R}, tree {actual:R}");
                }

                // The rule of the reference: inside a fit that fit; on a shared bound the lower of the two fits.
                var low = transport.Arrays.Fits[(start + fit) * TransportTable.FitStride];
                var high = transport.Arrays.Fits[(start + fit) * TransportTable.FitStride + 1];
                var picked = TransportSolver.FitOf(in view, start, count, temperature) - start;
                var ruled = temperature > low && temperature < high ? fit
                            : temperature == high && fit < count - 1 ? fit
                            : temperature == low && fit > 0 ? fit - 1
                            : -1;
                if (ruled >= 0 && picked != ruled)
                {
                    mismatches.Add($"{property.Name} at {temperature} K: the rule picked fit {picked}, expected {ruled} (fixture fit {fit})");
                }
            }
        }

        Assert.True(mismatches.Count == 0, $"{name}:\n" + string.Join("\n", mismatches));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Fit_intervals_are_those_of_the_file(string name)
    {
        var c = TransportHost.LoadFit(name);
        var species = c.Inputs.GetProperty("species").GetString()!;
        var partner = c.Inputs.GetProperty("partner").ValueKind == JsonValueKind.Null ? null : c.Inputs.GetProperty("partner").GetString();
        var (table, transport) = TablesFor(species, partner);
        var index = table.IndexOf(species);
        var expected = c.Inputs.GetProperty("fits").EnumerateArray()
            .Select(f => (Kind: f[0].GetString()!, Low: f[1].GetDouble(), High: f[2].GetDouble())).ToList();

        var actual = new List<(string Kind, double Low, double High)>();
        if (partner is null)
        {
            actual.AddRange(Bounds(transport, transport.Arrays.ViscosityStart[index], transport.Arrays.ViscosityCount[index], "V"));
            actual.AddRange(Bounds(transport, transport.Arrays.ConductivityStart[index], transport.Arrays.ConductivityCount[index], "C"));
        }
        else
        {
            var pair = transport.Arrays.PairIndex[index * table.SpeciesCount + table.IndexOf(partner)];
            Assert.True(pair >= 0, $"{name}: no pair data in the table");
            actual.AddRange(Bounds(transport, transport.Arrays.PairStart[pair], transport.Arrays.PairCount[pair], "V"));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Constant_terms_carry_the_si_factors()
    {
        var entry = fixture.Transport.Find("N2")!;
        var (_, transport) = TablesFor("N2", null);
        var fits = transport.Arrays.Fits;
        Assert.Equal(entry.Viscosity[0].D + Math.Log(1e-7), fits[5], 12);
        var conductivity = transport.Arrays.ConductivityStart[0];
        Assert.Equal(entry.Conductivity[0].D + Math.Log(1e-4), fits[conductivity * TransportTable.FitStride + 5], 12);
        Assert.Equal(1e-7, TransportTable.ViscosityFactorToSi);
        Assert.Equal(1e-4, TransportTable.ConductivityFactorToSi);
    }

    [Fact]
    public void Species_without_an_entry_have_no_fits_and_are_listed()
    {
        var c = TransportHost.LoadRocket("ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var (table, transport) = TransportHost.TablesOf(fixture, c);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var name = table.Species[j];
            var entry = j < table.GasCount ? fixture.Transport.Find(name) : null;
            var hasData = entry is not null && entry.Viscosity.Count > 0;
            Assert.Equal(hasData, transport.Arrays.ViscosityCount[j] > 0);
            Assert.Equal(hasData, transport.SpeciesWithData.Contains(name));
            Assert.Equal(!hasData && j < table.GasCount, transport.SpeciesWithoutData.Contains(name));
        }

        Assert.NotEmpty(transport.SpeciesWithoutData);
        Assert.NotEmpty(transport.SpeciesWithData);
    }

    [Fact]
    public void Pairs_are_those_of_the_database_with_both_species_gaseous_in_the_table()
    {
        var c = TransportHost.LoadRocket("nto-udmh_of2.2_pc2MPa_shiftingEquilibrium");
        var (table, transport) = TransportHost.TablesOf(fixture, c);
        var expected = fixture.Transport.Entries
            .Where(e => e.Partner is not null && e.Viscosity.Count > 0)
            .Where(e => IsGas(table, e.Species) && IsGas(table, e.Partner!))
            .Select(e => (e.Species, e.Partner!))
            .ToList();
        Assert.Equal(expected, transport.Pairs);
        Assert.Equal(expected.Count, transport.Arrays.PairTotal);
        var count = table.SpeciesCount;
        for (var p = 0; p < expected.Count; p++)
        {
            var a = table.IndexOf(expected[p].Item1);
            var b = table.IndexOf(expected[p].Item2);
            Assert.Equal(p, transport.Arrays.PairIndex[a * count + b]);
            Assert.Equal(p, transport.Arrays.PairIndex[b * count + a]);
        }

        var indexed = transport.Arrays.PairIndex.Count(i => i >= 0);
        Assert.Equal(2 * expected.Count, indexed);
    }

    private static bool IsGas(SpeciesTable table, string name)
    {
        var index = table.IndexOf(name);
        return index >= 0 && index < table.GasCount;
    }

    private static IEnumerable<(string Kind, double Low, double High)> Bounds(TransportTable transport, int start, int count, string kind)
    {
        for (var i = 0; i < count; i++)
        {
            var offset = (start + i) * TransportTable.FitStride;
            yield return (kind, transport.Arrays.Fits[offset], transport.Arrays.Fits[offset + 1]);
        }
    }

    private static (int Start, int Count) Run(TransportTable transport, string property, int species, int partner)
    {
        if (partner >= 0)
        {
            var pair = transport.Arrays.PairIndex[species * transport.SpeciesCount + partner];
            Assert.True(pair >= 0, "no pair data in the table");
            return (transport.Arrays.PairStart[pair], transport.Arrays.PairCount[pair]);
        }

        return property switch
        {
            "viscosity" => (transport.Arrays.ViscosityStart[species], transport.Arrays.ViscosityCount[species]),
            "conductivity" => (transport.Arrays.ConductivityStart[species], transport.Arrays.ConductivityCount[species]),
            _ => throw new InvalidOperationException($"unknown output {property}"),
        };
    }

    /// <summary>A species table holding just the species (and its partner), with the elements of their formulas.</summary>
    private (SpeciesTable Species, TransportTable Transport) TablesFor(string species, string? partner)
    {
        var names = partner is null ? new[] { species } : [species, partner];
        var elements = names.SelectMany(n => fixture.Database[n].Formula.Select(f => f.Symbol)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var table = SpeciesTable.Build(fixture.Database, elements, names);
        return (table, TransportTable.Build(fixture.Transport, table));
    }
}
