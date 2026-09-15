using System.Security.Cryptography;
using APThermo.Fixtures;

namespace APThermo.Data.Tests;

/// <summary>L1: the database embedded in the assembly (root BOOT.md, ## Delivery, Data) against the committed data/ files.</summary>
public sealed class BundledDatabaseTests
{
    [Fact]
    public void Embedded_resource_bytes_equal_the_committed_files()
    {
        AssertResourceEqualsFile("APThermo.Data.Bundled.thermo.inp", "thermo.inp");
        AssertResourceEqualsFile("APThermo.Data.Bundled.trans.inp", "trans.inp");
        AssertResourceEqualsFile("APThermo.Data.Bundled.NOTICE", "NOTICE");
    }

    [Fact]
    public void LoadBundled_equals_Load_on_every_species_and_coefficient()
    {
        var bundled = SpeciesDatabase.LoadBundled();
        var loaded = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));

        Assert.Equal(loaded.Provenance.ThermoSha256, bundled.Provenance.ThermoSha256);
        Assert.Equal(loaded.Provenance.TransSha256, bundled.Provenance.TransSha256);
        Assert.Equal(loaded.Provenance.HeaderDate, bundled.Provenance.HeaderDate);
        Assert.Equal(loaded.Provenance.DefaultIntervalBounds, bundled.Provenance.DefaultIntervalBounds);

        AssertSameSpecies(loaded.Products, bundled.Products);
        AssertSameSpecies(loaded.Reactants, bundled.Reactants);
        AssertSameTransport(loaded.Transport, bundled.Transport);
    }

    [Fact]
    public void BundledNotice_equals_the_committed_file()
    {
        var expected = File.ReadAllText(Path.Combine(RepositoryPaths.Data, "NOTICE"));
        Assert.Equal(expected, SpeciesDatabase.BundledNotice());
    }

    private static void AssertResourceEqualsFile(string resourceName, string fileName)
    {
        using var stream = typeof(SpeciesDatabase).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var memory = new MemoryStream();
        stream!.CopyTo(memory);
        var resourceHash = Convert.ToHexStringLower(SHA256.HashData(memory.ToArray()));
        var fileHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(RepositoryPaths.Data, fileName))));
        Assert.Equal(fileHash, resourceHash);
    }

    /// <summary>Field by field, because the record's default equality compares its list fields by reference, not by value.</summary>
    private static void AssertSameSpecies(IReadOnlyList<Species> expected, IReadOnlyList<Species> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var a = actual[i];
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.Comment, a.Comment);
            Assert.Equal(e.DateCode, a.DateCode);
            Assert.Equal(e.Formula, a.Formula);
            Assert.Equal(e.Phase, a.Phase);
            Assert.Equal(e.MolarMass, a.MolarMass);
            Assert.Equal(e.FormationEnthalpy, a.FormationEnthalpy);
            Assert.Equal(e.AssignedTemperature, a.AssignedTemperature);
            Assert.Equal(e.Section, a.Section);
            Assert.Equal(e.IsInert, a.IsInert);
            AssertSameIntervals(e.Intervals, a.Intervals);
        }
    }

    private static void AssertSameIntervals(IReadOnlyList<TemperatureInterval> expected, IReadOnlyList<TemperatureInterval> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var k = 0; k < expected.Count; k++)
        {
            var e = expected[k];
            var a = actual[k];
            Assert.Equal(e.TLow, a.TLow);
            Assert.Equal(e.THigh, a.THigh);
            Assert.Equal(e.Exponents, a.Exponents);
            Assert.Equal(e.Coefficients, a.Coefficients);
            Assert.Equal(e.B1, a.B1);
            Assert.Equal(e.B2, a.B2);
            Assert.Equal(e.EnthalpyOffset, a.EnthalpyOffset);
        }
    }

    private static void AssertSameTransport(TransportDatabase? expected, TransportDatabase? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected!.Entries.Count, actual!.Entries.Count);
        for (var i = 0; i < expected.Entries.Count; i++)
        {
            var e = expected.Entries[i];
            var a = actual.Entries[i];
            Assert.Equal(e.Species, a.Species);
            Assert.Equal(e.Partner, a.Partner);
            Assert.Equal(e.Reference, a.Reference);
            AssertSameFits(e.Viscosity, a.Viscosity);
            AssertSameFits(e.Conductivity, a.Conductivity);
        }
    }

    private static void AssertSameFits(IReadOnlyList<TransportFit> expected, IReadOnlyList<TransportFit> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].TLow, actual[i].TLow);
            Assert.Equal(expected[i].THigh, actual[i].THigh);
            Assert.Equal(expected[i].A, actual[i].A);
            Assert.Equal(expected[i].B, actual[i].B);
            Assert.Equal(expected[i].C, actual[i].C);
            Assert.Equal(expected[i].D, actual[i].D);
        }
    }
}
