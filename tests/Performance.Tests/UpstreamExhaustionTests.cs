using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>Controlled walk position reaches the real verifier with a physical locator.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class UpstreamExhaustionTests
{
    /// <summary>The final unresolved boundary refuses the candidate in the verifier itself.</summary>
    [Fact]
    public void VerifyRefusesAnUnresolvedFinalBoundary()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("throat", "li2o-throat_pc3MPa_h2.2375MJkg"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocket = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocket.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var physicalChamber));
        var chamber = new ChamberReference(physicalChamber.Pressure, -double.MaxValue / 4, physicalChamber.Entropy, physicalChamber.GammaS);
        var chamberFingerprint = CondensedSetIdentity.Single(0);
        for (var index = 0; index < context.Scratch.CondensedInSolution.Length; index++)
        {
            context.Scratch.CondensedInSolution[index] = -1;
        }
        context.Scratch.CondensedInSolution[0] = 1;
        context.Result.Stations[RocketSolver.Throat] = context.Result.Stations[RocketSolver.Chamber];
        var query = new ThroatQuery(in context, in chamber);
        var candidatePressure = chamber.Pressure * 0.9;
        var status = UpstreamChokeCheck.Verify(in query, chamberFingerprint, ref candidatePressure,
            UpstreamChokeCheck.MaxPhaseBoundaries - 1);
        Assert.Equal(CaseStatus.ThroatNotFound, status);
    }

    /// <summary>An already-resolved candidate is accepted at the same injected walk position.</summary>
    [Fact]
    public void VerifyAcceptsAnAlreadyResolvedCandidate()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("throat", "li2o-throat_pc3MPa_h2.2375MJkg"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocket = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocket.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var chamber));
        var fingerprint = ThroatBracketSearch.CondensedFingerprint(in context);
        context.Result.Stations[RocketSolver.Throat] = context.Result.Stations[RocketSolver.Chamber];
        var query = new ThroatQuery(in context, in chamber);
        var pressure = chamber.Pressure;
        Assert.Equal(CaseStatus.Ok, UpstreamChokeCheck.Verify(in query, fingerprint, ref pressure,
            UpstreamChokeCheck.MaxPhaseBoundaries - 1));
        Assert.Equal(chamber.Pressure, pressure);
    }
}
