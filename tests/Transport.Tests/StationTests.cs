using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Transport.Tests;

/// <summary>
/// L1: at every station of every rocket fixture run with transport, the solver evaluated on the reference composition reproduces
/// the reference's viscosity, conductivities, Prandtl numbers and the gas heat capacity of the transport set within the tolerance table.
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
    public void Stations_match_the_reference(string name)
    {
        var c = TransportHost.LoadRocket(name);
        var (table, transport) = TransportHost.TablesOf(fixture, c);
        using var speciesBuffers = SpeciesTableBuffers.Upload(fixture.Accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(fixture.Accelerator, transport);
        var stations = TransportHost.StationsWithTransport(c);
        Assert.NotEmpty(stations);
        var mismatches = new List<string>();
        foreach (var station in stations)
        {
            var label = station.GetProperty("station").GetString();
            var evaluation = TransportHost.Evaluate(fixture.Accelerator, speciesBuffers, transportBuffers,
                                                    station.GetProperty("temperature").GetDouble(), TransportHost.MolesOf(table, station));
            if (evaluation.Status != CaseStatus.Ok)
            {
                mismatches.Add($"{label}: status {evaluation.Status}");
                continue;
            }

            var figures = evaluation.Figures;
            var defective = figures.TraceEliminations > 0;
            foreach (var (field, value) in TransportHost.Figures)
            {
                var expected = station.GetProperty(field).GetDouble();
                var actual = value(figures);
                if (defective && TransportHost.ReactingFields.Contains(field))
                {
                    // The reference keeps the reaction through the trace species (Fixtures BOOT.md): its reacting conductivity is inflated.
                    if (field == "reactingConductivity" && fixture.Tolerances.Matches(field, expected, actual))
                    {
                        mismatches.Add($"{label} {field}: reference {expected:R} agrees with the tree's {actual:R}; the documented defect is gone");
                    }

                    continue;
                }

                if (!fixture.Tolerances.Matches(field, expected, actual))
                {
                    mismatches.Add($"{label} {field}: reference {expected:R}, tree {actual:R}");
                }
            }

            var cpFrozen = station.GetProperty("cpFrozen").GetDouble();
            if (!fixture.Tolerances.Matches("cpFrozen", cpFrozen, figures.FrozenHeatCapacity))
            {
                mismatches.Add($"{label} cpFrozen (transport set): reference {cpFrozen:R}, tree {figures.FrozenHeatCapacity:R}");
            }

            if (figures.ReactingConductivity < figures.FrozenConductivity)
            {
                mismatches.Add($"{label}: reacting conductivity {figures.ReactingConductivity:R} below the frozen {figures.FrozenConductivity:R}");
            }

            var gas = TransportHost.GasFractionsOf(table, station);
            var surelyEstimated = transport.SpeciesWithoutData.Any(s => gas.TryGetValue(s, out var x) && x >= SurelySelectedFraction);
            if (surelyEstimated && figures.EstimatedSpeciesCount == 0)
            {
                mismatches.Add($"{label}: a species without data above {SurelySelectedFraction} of the gas, yet nothing was estimated");
            }

            if (figures.EstimatedSpeciesCount == 0 != (figures.EstimatedMoleFraction == 0.0))
            {
                mismatches.Add($"{label}: {figures.EstimatedSpeciesCount} estimated species with mole fraction {figures.EstimatedMoleFraction:R}");
            }
        }

        Assert.True(mismatches.Count == 0, $"{name}:\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void The_trace_component_stations_carry_the_documented_reference_defect()
    {
        var defective = new List<string>();
        var stationsSeen = 0;
        foreach (var row in Cases())
        {
            var c = TransportHost.LoadRocket((string)row[0]);
            var (table, transport) = TransportHost.TablesOf(fixture, c);
            using var speciesBuffers = SpeciesTableBuffers.Upload(fixture.Accelerator, table);
            using var transportBuffers = TransportTableBuffers.Upload(fixture.Accelerator, transport);
            foreach (var station in TransportHost.StationsWithTransport(c))
            {
                stationsSeen++;
                var evaluation = TransportHost.Evaluate(fixture.Accelerator, speciesBuffers, transportBuffers,
                                                        station.GetProperty("temperature").GetDouble(), TransportHost.MolesOf(table, station));
                Assert.Equal(CaseStatus.Ok, evaluation.Status);
                if (evaluation.Figures.TraceEliminations == 0)
                {
                    continue;
                }

                var label = $"{c.Name} {station.GetProperty("station").GetString()}";
                defective.Add(label);
                var reference = station.GetProperty("reactingConductivity").GetDouble();
                var frozen = station.GetProperty("frozenConductivity").GetDouble();
                Assert.True(reference > DefectRatio * frozen, $"{label}: the reference's reacting conductivity {reference:R} is not inflated over the frozen {frozen:R}");
                Assert.True(evaluation.Figures.ReactingConductivity < reference, $"{label}: the tree's {evaluation.Figures.ReactingConductivity:R} is not below the reference");
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
    }
}
