using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Transport.Tests;

/// <summary>What one host call of the transport solver produced.</summary>
internal sealed record TransportEvaluation(CaseStatus Status, TransportFigures Figures);

/// <summary>Runs the transport solver on the host over CPU-accelerator buffers, and reads the fixtures it is checked against.</summary>
internal static class TransportHost
{
    /// <summary>The station outputs the transport node computes, by fixture name and figure field.</summary>
    public static readonly IReadOnlyList<(string Field, Func<TransportFigures, double> Value)> Figures =
    [
        ("viscosity", f => f.Viscosity),
        ("frozenConductivity", f => f.FrozenConductivity),
        ("reactingConductivity", f => f.ReactingConductivity),
        ("frozenPrandtl", f => f.FrozenPrandtl),
        ("reactingPrandtl", f => f.ReactingPrandtl),
    ];

    /// <summary>Fields that carry the reaction term: skipped where the reference's value is known to be defective (Fixtures BOOT.md).</summary>
    public static readonly IReadOnlySet<string> ReactingFields = new HashSet<string> { "reactingConductivity", "reactingPrandtl" };

    public static string[] ElementsOf(CeaCase c) => c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();

    public static string[] ProductsOf(CeaCase c) => c.Inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();

    public static bool HasTransport(CeaCase c) => c.Inputs.GetProperty("transport").GetBoolean();

    /// <summary>A key identifying the table a case needs: cases with equal keys can share a batch.</summary>
    public static string TableKey(CeaCase c) => string.Join(",", ElementsOf(c)) + "|" + string.Join(",", ProductsOf(c));

    /// <summary>The stations of a rocket fixture that carry transport outputs.</summary>
    public static IReadOnlyList<JsonElement> StationsWithTransport(CeaCase c) =>
        c.Outputs.GetProperty("stations").EnumerateArray().Where(s => s.TryGetProperty("viscosity", out _)).ToList();

    /// <summary>The station's composition in kmol per kg: mole fractions over all species divided by the reference's MW.</summary>
    public static double[] MolesOf(SpeciesTable table, JsonElement station)
    {
        var moles = new double[table.SpeciesCount];
        var mixtureMolarMass = station.GetProperty("mixtureMolarMass").GetDouble();
        foreach (var species in station.GetProperty("moleFractions").EnumerateObject())
        {
            var indices = table.IndicesOf(species.Name);
            if (indices.Count == 0)
            {
                throw new InvalidOperationException($"{species.Name} is not in the table");
            }

            // The whole fraction on the first piece of a cut species: the solver reads only the gases, which never split.
            moles[indices[0]] = species.Value.GetDouble() / mixtureMolarMass;
        }

        return moles;
    }

    /// <summary>The gaseous mole fraction of the station, relative to the gaseous moles, by species name.</summary>
    public static Dictionary<string, double> GasFractionsOf(SpeciesTable table, JsonElement station)
    {
        var gas = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var species in station.GetProperty("moleFractions").EnumerateObject())
        {
            var indices = table.IndicesOf(species.Name);
            if (indices.Count > 0 && indices[0] < table.GasCount)
            {
                gas[species.Name] = species.Value.GetDouble();
            }
        }

        var total = gas.Values.Sum();
        return gas.ToDictionary(kv => kv.Key, kv => kv.Value / total, StringComparer.Ordinal);
    }

    public static (SpeciesTable Species, TransportTable Transport) TablesOf(CpuFixture fixture, CeaCase c)
    {
        var species = SpeciesTable.Build(fixture.Database, ElementsOf(c), ProductsOf(c));
        return (species, TransportTable.Build(fixture.Transport, species));
    }

    /// <summary>One host evaluation of a station.</summary>
    public static TransportEvaluation Evaluate(Accelerator accelerator, SpeciesTable species, TransportTable transport, double temperature, double[] moles)
    {
        using var speciesBuffers = SpeciesTableBuffers.Upload(accelerator, species);
        using var transportBuffers = TransportTableBuffers.Upload(accelerator, transport);
        return Evaluate(accelerator, speciesBuffers, transportBuffers, temperature, moles);
    }

    public static TransportEvaluation Evaluate(Accelerator accelerator, SpeciesTableBuffers species, TransportTableBuffers transport,
                                               double temperature, double[] moles)
    {
        var speciesCount = species.Table.SpeciesCount;
        var elementCount = species.Table.ElementCount;
        using var molesBuffer = accelerator.Allocate1D(moles);
        using var doubles = accelerator.Allocate1D<double>(TransportLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        using var figures = accelerator.Allocate1D<TransportFigures>(1);
        var scratch = TransportScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var speciesView = species.View;
        var transportView = transport.View;
        var status = TransportSolver.Evaluate(in speciesView, in transportView, temperature, molesBuffer.View, in scratch, figures.View);
        return new TransportEvaluation(status, figures.GetAsArray1D()[0]);
    }

    /// <summary>The rocket fixture files run with transport, as theory data: the file name without extension.</summary>
    public static IEnumerable<object[]> RocketCasesWithTransport() =>
        FixtureFiles.Enumerate("rocket")
            .Where(path => HasTransport(CeaFixtures.Load(path)))
            .Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });

    public static CeaCase LoadRocket(string name) => CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "rocket", name + ".json"));

    /// <summary>The transport fit fixture files as theory data.</summary>
    public static IEnumerable<object[]> FitCases() =>
        FixtureFiles.Enumerate("transport").Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });

    public static CeaCase LoadFit(string name) => CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "transport", name + ".json"));
}
