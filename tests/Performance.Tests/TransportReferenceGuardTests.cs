using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Fixtures;

namespace APThermo.Performance.Tests;

/// <summary>Reference gas-phase caveats cannot hide arbitrary changed heat capacities.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TransportReferenceGuardTests
{
    /// <summary>A real reference with transport and condensed species is compared successfully.</summary>
    [Fact]
    public void TheKnownGasPhaseReferenceRemainsComparable()
    {
        var reference = RocketHost.Load("ap-htpb-al_pc5MPa_shiftingEquilibrium");
        var actual = RocketHost.Solve(CpuFixture.Shared, reference);
        var tolerances = ToleranceTable.Load();
        var chamber = RocketInputs.FixtureStationsOf(reference)[0];
        var referenceCp = chamber.GetProperty("cpFrozen").GetDouble();
        var referenceCv = chamber.GetProperty("cvFrozen").GetDouble();
        Assert.False(tolerances.Matches("cpFrozen", referenceCp, actual.Outcome.Stations[0].CpFrozen));
        Assert.True(ReferenceTransportHeatCapacity.HasGasPhaseSignature(chamber, actual.Table, "cpFrozen", referenceCp, tolerances));
        Assert.True(ReferenceTransportHeatCapacity.HasGasPhaseSignature(chamber, actual.Table, "cvFrozen", referenceCv, tolerances));
        Assert.Empty(StationComparison.Compare(reference, actual, tolerances));
    }

    /// <summary>Corrupting either field must be reported instead of being exempted unconditionally.</summary>
    [Theory]
    [InlineData("cpFrozen")]
    [InlineData("cvFrozen")]
    public void AnArbitraryHeatCapacityIsNotAGasPhaseDefectSignature(string field)
    {
        var reference = RocketHost.Load("ap-htpb-al_pc5MPa_shiftingEquilibrium");
        var actual = RocketHost.Solve(CpuFixture.Shared, reference);
        var outputs = JsonNode.Parse(reference.Outputs.GetRawText())!;
        outputs["stations"]![0]![field] = 1000000.0;
        var corrupt = reference with { Outputs = JsonSerializer.SerializeToElement(outputs) };
        Assert.Contains(StationComparison.Compare(corrupt, actual, ToleranceTable.Load()),
            mismatch => mismatch.Contains(field, StringComparison.Ordinal));
    }
}
