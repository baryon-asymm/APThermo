using System.Text.Json;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

/// <summary>L0: the table holds the fits of the file in SI and evaluates them as the independent Python evaluation does.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class FitTests
{
    /// <summary>The transport fit fixture files as theory data, delegating to <see cref="TransportHost.FitCases"/>.</summary>
    public static TheoryData<string> Cases() => TransportHost.FitCases();

    /// <summary>Fit values match the independent evaluation.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void FitValuesMatchTheIndependentEvaluation(string name)
    {
        var c = TransportHost.LoadFit(name);
        var species = c.Inputs.GetProperty("species").GetString()!;
        var partner = c.Inputs.GetProperty("partner").ValueKind == JsonValueKind.Null ? null : c.Inputs.GetProperty("partner").GetString();
        var (table, transport) = TablesFor(species, partner);
        using var speciesBuffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        using var buffers = TransportTableBuffers.Upload(CpuFixture.Shared.Accelerator, transport);
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
                if (!CpuFixture.Shared.Tolerances.Matches("transportFit", expected, actual))
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

    /// <summary>Fit intervals are those of the file.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void FitIntervalsAreThoseOfTheFile(string name)
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

    /// <summary>Constant terms carry the SI factors.</summary>
    [Fact]
    public void ConstantTermsCarryTheSiFactors()
    {
        var entry = CpuFixture.Shared.Transport.Find("N2")!;
        var (_, transport) = TablesFor("N2", null);
        var fits = transport.Arrays.Fits;
        Assert.Equal(entry.Viscosity[0].D + Math.Log(1e-7), fits[5], 12);
        var conductivity = transport.Arrays.ConductivityStart[0];
        Assert.Equal(entry.Conductivity[0].D + Math.Log(1e-4), fits[conductivity * TransportTable.FitStride + 5], 12);
        Assert.Equal(1e-7, TransportTable.ViscosityFactorToSi);
        Assert.Equal(1e-4, TransportTable.ConductivityFactorToSi);
    }

    /// <summary>Species without an entry have no fits and are listed.</summary>
    [Fact]
    public void SpeciesWithoutAnEntryHaveNoFitsAndAreListed()
    {
        var c = TransportHost.LoadRocket("ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, c);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var name = table.Species[j];
            var entry = j < table.GasCount ? CpuFixture.Shared.Transport.Find(name) : null;
            var hasData = entry is not null && entry.Viscosity.Count > 0;
            Assert.Equal(hasData, transport.Arrays.ViscosityCount[j] > 0);
            Assert.Equal(hasData, transport.SpeciesWithData.Contains(name));
            Assert.Equal(!hasData && j < table.GasCount, transport.SpeciesWithoutData.Contains(name));
        }

        Assert.NotEmpty(transport.SpeciesWithoutData);
        Assert.NotEmpty(transport.SpeciesWithData);
    }

    /// <summary>Pairs are those of the database with both species gaseous in the table.</summary>
    [Fact]
    public void PairsAreThoseOfTheDatabaseWithBothSpeciesGaseousInTheTable()
    {
        var c = TransportHost.LoadRocket("nto-udmh_of2.2_pc2MPa_shiftingEquilibrium");
        var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, c);
        var expected = CpuFixture.Shared.Transport.Entries
            .Where(e => e.Partner is not null && e.Viscosity.Count > 0)
            .Where(e => IsGas(table, e.Species) && IsGas(table, e.Partner!))
            .Select(e => (e.Species, Partner: e.Partner!))
            .ToList();
        Assert.Equal(expected, transport.Pairs);
        Assert.Equal(expected.Count, transport.Arrays.PairTotal);
        var count = table.SpeciesCount;
        for (var p = 0; p < expected.Count; p++)
        {
            var a = table.IndexOf(expected[p].Species);
            var b = table.IndexOf(expected[p].Partner);
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
    private static (SpeciesTable Species, TransportTable Transport) TablesFor(string species, string? partner)
    {
        var names = partner is null ? new[] { species } : [species, partner];
        var elements = names.SelectMany(n => CpuFixture.Shared.Database[n].Formula.Select(f => f.Symbol)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, elements, names);
        return (table, TransportTable.Build(CpuFixture.Shared.Transport, table));
    }
}
