using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

/// <summary>
/// L1: at every station of every rocket fixture run with transport, the solver evaluated on the reference composition reproduces
/// the reference's viscosity, conductivities, Prandtl numbers and the gas heat capacity of the transport set within the tolerance
/// table; and the node's own invariants (the defect at a trace elimination, the reacting conductivity never below the frozen,
/// the estimated-species bookkeeping) hold beyond what the reference can check.
/// </summary>
public sealed class StationTests
{
    /// <summary>The reference's reacting conductivity at a defective station is at least this many times its frozen one (Fixtures BOOT.md).</summary>
    public const double DefectRatio = 10.0;

    /// <summary>A gaseous species without data carrying this fraction of the gas cannot stay outside the transport set.</summary>
    public const double SurelySelectedFraction = 1e-3;

    /// <summary>The rocket fixture files run with transport, as theory data, delegating to <see cref="TransportHost.RocketCasesWithTransport"/>.</summary>
    public static TheoryData<string> Cases() => TransportHost.RocketCasesWithTransport();

    /// <summary>Station figures match the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void StationFiguresMatchTheReference(string name)
    {
        var stations = TransportHost.EvaluateStations(CpuFixture.Shared, TransportHost.LoadRocket(name));
        Assert.NotEmpty(stations);
        var mismatches = stations.SelectMany(s => FigureComparison.Mismatches(s, CpuFixture.Shared.Tolerances)).ToList();
        Assert.True(mismatches.Count == 0, $"{name}:\n" + string.Join("\n", mismatches));
    }

    /// <summary>The reference cpFrozen is the transport set heat capacity.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheReferenceCpFrozenIsTheTransportSetHeatCapacity(string name)
    {
        var stations = TransportHost.EvaluateStations(CpuFixture.Shared, TransportHost.LoadRocket(name));
        Assert.NotEmpty(stations);
        var mismatches = new List<string>();
        foreach (var evaluated in stations.Where(s => s.Evaluation.Status == CaseStatus.Ok))
        {
            var expected = evaluated.Station.GetProperty("cpFrozen").GetDouble();
            var actual = evaluated.Evaluation.Figures.FrozenHeatCapacity;
            if (!CpuFixture.Shared.Tolerances.Matches("cpFrozen", expected, actual))
            {
                mismatches.Add($"{evaluated.Label} cpFrozen (transport set): reference {expected:R}, tree {actual:R}");
            }
        }

        Assert.True(mismatches.Count == 0, $"{name}:\n" + string.Join("\n", mismatches));
    }

    /// <summary>Reacting conductivity is never below the frozen one.</summary>
    [Fact]
    public void ReactingConductivityIsNeverBelowTheFrozenOne()
    {
        var mismatches = new List<string>();
        foreach (var name in TransportHost.RocketCaseNamesWithTransport())
        {
            var c = TransportHost.LoadRocket(name);
            foreach (var evaluated in TransportHost.EvaluateStations(CpuFixture.Shared, c).Where(s => s.Evaluation.Status == CaseStatus.Ok))
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

    /// <summary>The trace component stations carry the documented reference defect.</summary>
    [Fact]
    public void TheTraceComponentStationsCarryTheDocumentedReferenceDefect()
    {
        var defective = new List<string>();
        var stationsSeen = 0;
        foreach (var name in TransportHost.RocketCaseNamesWithTransport())
        {
            var c = TransportHost.LoadRocket(name);
            foreach (var evaluated in TransportHost.EvaluateStations(CpuFixture.Shared, c))
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

    /// <summary>Species without data are estimated on the aluminized propellant.</summary>
    [Fact]
    public void SpeciesWithoutDataAreEstimatedOnTheAluminizedPropellant()
    {
        var c = TransportHost.LoadRocket("ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, c);
        var station = TransportHost.StationsWithTransport(c)[0];
        var evaluation = TransportHost.Evaluate(CpuFixture.Shared.Accelerator, table, transport, station.GetProperty("temperature").GetDouble(),
                                                TransportHost.MolesOf(table, station));
        Assert.Equal(CaseStatus.Ok, evaluation.Status);
        Assert.True(evaluation.Figures.EstimatedSpeciesCount > 0);
        Assert.InRange(evaluation.Figures.EstimatedMoleFraction, 1e-3, 0.1);
        Assert.Equal(TransportLayout.MaxSpecies, evaluation.Figures.SpeciesCount);
        Assert.Equal(1, evaluation.Figures.Capped);

        var mismatches = new List<string>();
        foreach (var name in TransportHost.RocketCaseNamesWithTransport())
        {
            mismatches.AddRange(EstimationMismatches(TransportHost.LoadRocket(name)));
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    /// <summary>
    /// Every station of one fixture: a species without data carrying <see cref="SurelySelectedFraction"/> or more of the gas
    /// cannot have been left out of the estimate, and the estimated count and mole fraction must agree on whether anything was
    /// estimated. The estimated-species consistency check the reference cannot settle (Transport.Tests BOOT.md, F-TK-05).
    /// </summary>
    private static List<string> EstimationMismatches(CeaCase c)
    {
        var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, c);
        var mismatches = new List<string>();
        foreach (var evaluated in TransportHost.EvaluateStations(CpuFixture.Shared, c, table, transport).Where(s => s.Evaluation.Status == CaseStatus.Ok))
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
