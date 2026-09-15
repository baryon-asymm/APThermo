using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

/// <summary>
/// L1: at every station of every rocket fixture run with transport, the solver evaluated on the reference composition reproduces
/// the reference's viscosity, conductivities, Prandtl numbers and the gas heat capacity of the transport set within the tolerance
/// table; and the node's own invariants (the defect at a trace elimination, the reacting conductivity never below the frozen,
/// the estimated-species bookkeeping) hold beyond what the reference can check.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class StationTests(CpuFixture fixture)
{
    /// <summary>The reference's reacting conductivity at a defective station is at least this many times its frozen one (Fixtures BOOT.md).</summary>
    public const double DefectRatio = 10.0;

    /// <summary>A gaseous species without data carrying this fraction of the gas cannot stay outside the transport set.</summary>
    public const double SurelySelectedFraction = 1e-3;

    public static IEnumerable<object[]> Cases() => TransportHost.RocketCasesWithTransport();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Station_figures_match_the_reference(string name)
    {
        var stations = TransportHost.EvaluateStations(fixture, TransportHost.LoadRocket(name));
        Assert.NotEmpty(stations);
        var mismatches = stations.SelectMany(s => FigureComparison.Mismatches(s, fixture.Tolerances)).ToList();
        Assert.True(mismatches.Count == 0, $"{name}:\n" + string.Join("\n", mismatches));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_reference_cpFrozen_is_the_transport_set_heat_capacity(string name)
    {
        var stations = TransportHost.EvaluateStations(fixture, TransportHost.LoadRocket(name));
        Assert.NotEmpty(stations);
        var mismatches = new List<string>();
        foreach (var evaluated in stations.Where(s => s.Evaluation.Status == CaseStatus.Ok))
        {
            var expected = evaluated.Station.GetProperty("cpFrozen").GetDouble();
            var actual = evaluated.Evaluation.Figures.FrozenHeatCapacity;
            if (!fixture.Tolerances.Matches("cpFrozen", expected, actual))
            {
                mismatches.Add($"{evaluated.Label} cpFrozen (transport set): reference {expected:R}, tree {actual:R}");
            }
        }

        Assert.True(mismatches.Count == 0, $"{name}:\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void Reacting_conductivity_is_never_below_the_frozen_one()
    {
        var mismatches = new List<string>();
        foreach (var row in Cases())
        {
            var c = TransportHost.LoadRocket((string)row[0]);
            foreach (var evaluated in TransportHost.EvaluateStations(fixture, c).Where(s => s.Evaluation.Status == CaseStatus.Ok))
            {
                var figures = evaluated.Evaluation.Figures;
                if (figures.ReactingConductivity < figures.FrozenConductivity)
                {
                    mismatches.Add($"{evaluated.Label}: reacting conductivity {figures.ReactingConductivity:R} below the frozen {figures.FrozenConductivity:R}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    [Fact]
    public void The_trace_component_stations_carry_the_documented_reference_defect()
    {
        var defective = new List<string>();
        var stationsSeen = 0;
        foreach (var row in Cases())
        {
            var c = TransportHost.LoadRocket((string)row[0]);
            foreach (var evaluated in TransportHost.EvaluateStations(fixture, c))
            {
                stationsSeen++;
                Assert.Equal(CaseStatus.Ok, evaluated.Evaluation.Status);
                if (evaluated.Evaluation.Figures.TraceEliminations == 0)
                {
                    continue;
                }

                defective.Add(evaluated.Label);
                var reference = evaluated.Station.GetProperty("reactingConductivity").GetDouble();
                var frozen = evaluated.Station.GetProperty("frozenConductivity").GetDouble();
                Assert.True(reference > DefectRatio * frozen, $"{evaluated.Label}: the reference's reacting conductivity {reference:R} is not inflated over the frozen {frozen:R}");
                Assert.True(evaluated.Evaluation.Figures.ReactingConductivity < reference, $"{evaluated.Label}: the tree's {evaluated.Evaluation.Figures.ReactingConductivity:R} is not below the reference");
            }
        }

        Assert.True(stationsSeen > 0);
        Assert.True(defective.Count > 0, "the documented reference defect no longer occurs: remove the caveat from the Fixtures BOOT.md and this test");
    }

    [Fact]
    public void Species_without_data_are_estimated_on_the_aluminized_propellant()
    {
        var c = TransportHost.LoadRocket("ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var (table, transport) = TransportHost.TablesOf(fixture, c);
        var station = TransportHost.StationsWithTransport(c)[0];
        var evaluation = TransportHost.Evaluate(fixture.Accelerator, table, transport, station.GetProperty("temperature").GetDouble(),
                                                TransportHost.MolesOf(table, station));
        Assert.Equal(CaseStatus.Ok, evaluation.Status);
        Assert.True(evaluation.Figures.EstimatedSpeciesCount > 0);
        Assert.InRange(evaluation.Figures.EstimatedMoleFraction, 1e-3, 0.1);
        Assert.Equal(TransportLayout.MaxSpecies, evaluation.Figures.SpeciesCount);
        Assert.Equal(1, evaluation.Figures.Capped);

        var mismatches = new List<string>();
        foreach (var row in Cases())
        {
            mismatches.AddRange(EstimationMismatches(TransportHost.LoadRocket((string)row[0])));
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    /// <summary>
    /// Every station of one fixture: a species without data carrying <see cref="SurelySelectedFraction"/> or more of the gas
    /// cannot have been left out of the estimate, and the estimated count and mole fraction must agree on whether anything was
    /// estimated. The estimated-species consistency check the reference cannot settle (Transport.Tests BOOT.md, F-TK-05).
    /// </summary>
    private IReadOnlyList<string> EstimationMismatches(CeaCase c)
    {
        var (table, transport) = TransportHost.TablesOf(fixture, c);
        var mismatches = new List<string>();
        foreach (var evaluated in TransportHost.EvaluateStations(fixture, c, table, transport).Where(s => s.Evaluation.Status == CaseStatus.Ok))
        {
            var figures = evaluated.Evaluation.Figures;
            var gas = TransportHost.GasFractionsOf(table, evaluated.Station);
            var surelyEstimated = transport.SpeciesWithoutData.Any(name => gas.TryGetValue(name, out var x) && x >= SurelySelectedFraction);
            if (surelyEstimated && figures.EstimatedSpeciesCount == 0)
            {
                mismatches.Add($"{evaluated.Label}: a species without data above {SurelySelectedFraction} of the gas, yet nothing was estimated");
            }

            if (figures.EstimatedSpeciesCount == 0 != (figures.EstimatedMoleFraction == 0.0))
            {
                mismatches.Add($"{evaluated.Label}: {figures.EstimatedSpeciesCount} estimated species with mole fraction {figures.EstimatedMoleFraction:R}");
            }
        }

        return mismatches;
    }
}
