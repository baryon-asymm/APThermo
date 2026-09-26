using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Transport.Tests;

/// <summary>
/// The pinned trace-eliminated stations (<c>BOOT.md</c>, the ⚠ of 2026-09-26, guards audit F1). Before this class, the only
/// guard on a station where <see cref="TransportFigures.TraceEliminations"/> is positive was
/// <c>StationTests.TheTraceComponentStationsCarryTheDocumentedReferenceDefect</c>'s <c>defective.Count &gt; 0</c>, which a
/// wrong reacting conductivity there moved only in the bit snapshot (excluded from the hosted matrix). Here an elimination
/// outside <see cref="TraceEliminatedStations.Keys"/>, or a listed station whose reference has stopped showing the defect's
/// signature, fails; and, at the listed stations, the tree's own reacting fields are checked against their definitions,
/// independent of the reference. None of these facts carries the <c>BitSnapshot</c> trait, so they run in the hosted matrix.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceEliminationGuardTests
{
    /// <summary>Only the pinned stations eliminate a trace species.</summary>
    [Fact]
    public void OnlyThePinnedStationsEliminateATraceSpecies()
    {
        var pinned = TraceEliminatedStations.Keys.ToHashSet();
        var seen = new HashSet<(string FixturePath, string Station)>();
        var unlisted = new List<string>();
        var stationsSeen = 0;
        foreach (var name in TransportHost.RocketCaseNamesWithTransport())
        {
            var c = TransportHost.LoadRocket(name);
            var fixturePath = RelativePath(c);
            var stations = TransportHost.StationsWithTransport(c);
            var evaluations = TransportHost.EvaluateStations(CpuFixture.Shared, c);
            for (var i = 0; i < stations.Count; i++)
            {
                stationsSeen++;
                if (evaluations[i].Evaluation.Figures.TraceEliminations == 0)
                {
                    continue;
                }

                var station = stations[i].GetProperty("station").GetString()!;
                var key = (fixturePath, station);
                _ = seen.Add(key);
                if (!pinned.Contains(key))
                {
                    unlisted.Add($"{fixturePath}|{station}: eliminates a trace species and is not on the pinned list");
                }
            }
        }

        Assert.True(stationsSeen > 0);
        foreach (var (staleFixturePath, staleStation) in pinned.Except(seen))
        {
            unlisted.Add($"{staleFixturePath}|{staleStation}: pinned, but the tree no longer eliminates a trace species there");
        }

        Assert.True(unlisted.Count == 0, string.Join("\n", unlisted));
    }

    /// <summary>Every pinned station shows the reference's defect signature.</summary>
    [Fact]
    public void EveryPinnedStationShowsTheReferenceDefectSignature()
    {
        Assert.NotEmpty(TraceEliminatedStations.Keys);
        var mismatches = new List<string>();
        foreach (var key in TraceEliminatedStations.Keys)
        {
            var station = LoadStation(key);
            var reference = station.GetProperty("reactingConductivity").GetDouble();
            var frozen = station.GetProperty("frozenConductivity").GetDouble();
            if (!(reference > StationTests.DefectRatio * frozen))
            {
                mismatches.Add($"{key.FixturePath}|{key.Station}: reference reacting conductivity {reference:R} is not " +
                                $"{StationTests.DefectRatio}x the frozen {frozen:R}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    /// <summary>
    /// At the pinned stations the tree's own reacting fields match their definitions, independent of the reference:
    /// <see cref="TransportFigures.ReactionCount"/> is 0, the reacting conductivity is the frozen one plus the reaction term
    /// (recomputed through <see cref="MixtureRules"/> and <see cref="ReactionTerms"/> directly), and the reacting Prandtl
    /// number is viscosity times the equilibrium heat capacity over the reacting conductivity.
    /// </summary>
    [Fact]
    public void AtThePinnedStationsTheReactingFieldsMatchTheirDefinitions()
    {
        Assert.NotEmpty(TraceEliminatedStations.Keys);
        var mismatches = new List<string>();
        foreach (var (fixturePath, stationName) in TraceEliminatedStations.Keys)
        {
            var c = CeaFixtures.Load(Path.Combine(RepositoryPaths.Root, fixturePath.Replace('/', Path.DirectorySeparatorChar)));
            var station = FindStation(c, stationName);
            var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, c);
            var moles = TransportHost.MolesOf(table, station);
            var temperature = station.GetProperty("temperature").GetDouble();
            var (figures, expectedReactingConductivity) = EvaluateAndRecomputeReactionTerm(table, transport, moles, temperature);

            var label = $"{fixturePath}|{stationName}";
            if (figures.ReactionCount != 0)
            {
                mismatches.Add($"{label}: ReactionCount is {figures.ReactionCount}, not 0");
            }

            if (!Bits.Same(figures.ReactingConductivity, expectedReactingConductivity))
            {
                mismatches.Add($"{label}: reacting conductivity {figures.ReactingConductivity:R} is not the frozen " +
                                $"conductivity plus the independently recomputed reaction term {expectedReactingConductivity:R}");
            }

            var expectedPrandtl = figures.Viscosity * figures.EquilibriumHeatCapacity / figures.ReactingConductivity;
            if (!Bits.Same(figures.ReactingPrandtl, expectedPrandtl))
            {
                mismatches.Add($"{label}: reacting Prandtl {figures.ReactingPrandtl:R} is not viscosity times the " +
                                $"equilibrium heat capacity over the reacting conductivity ({expectedPrandtl:R})");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    /// <summary>Runs the full evaluation, then recomputes the mixture and reaction stages directly on the same scratch to get
    /// an independent frozen-conductivity-plus-reaction-term figure.</summary>
    private static (TransportFigures Figures, double ExpectedReactingConductivity) EvaluateAndRecomputeReactionTerm(
        SpeciesTable table, TransportTable transport, double[] moles, double temperature)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var speciesBuffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(accelerator, transport);
        using var molesBuffer = accelerator.Allocate1D(moles);
        using var doubles = accelerator.Allocate1D<double>(TransportLayout.DoublesPerCase(elementCount));
        using var ints = accelerator.Allocate1D<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        using var figuresBuffer = accelerator.Allocate1D<TransportFigures>(1);
        var scratch = TransportScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var speciesView = speciesBuffers.View;
        var transportView = transportBuffers.View;
        var status = TransportSolver.Evaluate(in speciesView, in transportView, temperature, molesBuffer.View, in scratch, figuresBuffer.View);
        Assert.Equal(CaseStatus.Ok, status);
        var figures = figuresBuffer.GetAsArray1D()[0];

        // The same scratch, still holding what the full evaluation left in it: Eta, Cond, Xs (MixtureRules) and Alpha, H
        // (ReactionTerms) are read-only from here on, so re-running the two stages reproduces their contributions independently
        // of SetProperties.Fill, the stage the guards audit's mutation (k_r = 3 * k_f) targets.
        var inputs = new StationInputs(in speciesView, in transportView, in scratch, molesBuffer.View, temperature);
        var mixture = MixtureRules.Evaluate(in inputs, figures.SpeciesCount);
        var reaction = ReactionTerms.Evaluate(in inputs, figures.SpeciesCount, figures.ReactionCount);
        Assert.Equal(CaseStatus.Ok, reaction.Status);
        return (figures, mixture.FrozenConductivity + reaction.Conductivity);
    }

    private static JsonElement FindStation(CeaCase c, string name) =>
        TransportHost.StationsWithTransport(c).First(s => s.GetProperty("station").GetString() == name);

    private static JsonElement LoadStation((string FixturePath, string Station) key)
    {
        var c = CeaFixtures.Load(Path.Combine(RepositoryPaths.Root, key.FixturePath.Replace('/', Path.DirectorySeparatorChar)));
        return FindStation(c, key.Station);
    }

    private static string RelativePath(CeaCase c) => Path.GetRelativePath(RepositoryPaths.Root, c.Path).Replace('\\', '/');
}
