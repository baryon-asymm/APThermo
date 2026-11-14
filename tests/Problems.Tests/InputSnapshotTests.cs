using APThermo.Data;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>Input records and completed outputs must own nested collection snapshots.</summary>
[Collection(SolverFixture.CollectionName)]
public sealed class InputSnapshotTests
{
    /// <summary>Public record constructors and init accessors detach caller-owned collections.</summary>
    [Fact]
    public void RecordsSnapshotTheirInputCollections()
    {
        ElementCount[] formula = [new("H", 2.0)];
        var definition = new CustomReactantDefinition(formula, 0.0, 298.15);
        formula[0] = new ElementCount("O", 1.0);
        Assert.Equal("H", definition.Formula[0].Symbol);
        double[] exits = [2.0];
        var rocket = new RocketProblem { AreaRatios = exits };
        exits[0] = 3.0;
        Assert.Equal(2.0, rocket.AreaRatios[0]);
        var composition = new Dictionary<string, double> { ["H"] = 1.0 };
        var state = new StateRecord(100000.0, composition, Temperature: 1000.0) { AreaRatios = exits };
        composition["H"] = 2.0;
        exits[0] = 4.0;
        Assert.Equal(1.0, state.Composition["H"]);
        Assert.Equal(3.0, state.AreaRatios[0]);
        string[] omit = ["H2"];
        var options = new StateBatchOptions(Omit: omit);
        omit[0] = "H";
        Assert.Equal("H2", options.Omit![0]);
    }

    /// <summary>With expressions take snapshots too, instead of reopening the original alias.</summary>
    [Fact]
    public void WithInitializersAlsoSnapshotCollections()
    {
        double[] values = [2.0];
        var original = new RocketProblem { AreaRatios = [1.5] };
        var copy = original with { AreaRatios = values };
        values[0] = 3.0;
        Assert.Equal(2.0, copy.AreaRatios[0]);
        Assert.Equal(1.5, original.AreaRatios[0]);
        ElementCount[] formula = [new("H", 2.0)];
        var definition = new CustomReactantDefinition([], 0.0, 298.15) with { Formula = formula };
        formula[0] = new ElementCount("O", 1.0);
        Assert.Equal("H", definition.Formula[0].Symbol);
    }

    /// <summary>Built definitions and mixtures expose no mutable list or dictionary views.</summary>
    [Fact]
    public void DefinitionsExposeReadOnlyViews()
    {
        var propellant = Propellant.From(SolverFixture.Shared.Database).Named("O2", 1.0).Build();
        var elements = Assert.IsType<IList<string>>(propellant.Elements, exactMatch: false);
        _ = Assert.Throws<NotSupportedException>(() => elements[0] = "H");
        var mixture = ElementalMixture.Create(new Dictionary<string, double> { ["O"] = 1.0 });
        var moles = Assert.IsType<IDictionary<string, double>>(mixture.ElementMoles, exactMatch: false);
        _ = Assert.Throws<NotSupportedException>(() => moles["O"] = 2.0);
        var candidates = SolverFixture.Shared.Solver.CandidateSpeciesFor(["H"]);
        var list = Assert.IsType<IList<string>>(candidates, exactMatch: false);
        _ = Assert.Throws<NotSupportedException>(() => list[0] = "bad");
    }

    /// <summary>Completed results, including Station with-initializers, detach all source collections.</summary>
    [Fact]
    public void PublishedResultsSnapshotCollections()
    {
        var moles = new Dictionary<string, double> { ["H2"] = 1.0 };
        var solids = new Dictionary<string, double> { ["solid"] = 0.25 };
        var station = new Station("state", default, null, moles, solids, null, null, CaseStatus.Ok);
        moles["H2"] = 0.5;
        solids["solid"] = 0.5;
        Assert.Equal(1.0, station.MoleFractions["H2"]);
        Assert.Equal(0.25, station.CondensedMassFractions["solid"]);
        var copy = station with { MoleFractions = moles };
        moles["H2"] = 0.75;
        Assert.Equal(0.5, copy.MoleFractions["H2"]);
        var species = new List<string> { "H2" };
        var stations = new List<Station> { station };
        var mixture = ElementalMixture.Create(new Dictionary<string, double> { ["H"] = 1.0 });
        var result = new RocketResult(null, mixture, 1.0, new RocketProblem(), null, species, stations,
            CaseStatus.Ok, SolverFixture.Shared.Solver.Accelerator);
        species.Clear();
        stations.Clear();
        _ = Assert.Single(result.Species);
        _ = Assert.Single(result.Stations);
    }
}
