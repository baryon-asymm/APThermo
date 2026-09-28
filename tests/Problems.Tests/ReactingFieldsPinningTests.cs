using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>
/// L0: the second hidden-defect audit of 2026-09-28 (guards part, observation O1). <see cref="ReferenceComparison"/>
/// skips the reacting conductivity and heat capacity of a station wherever the tree's own solve reports
/// <c>TraceEliminations &gt; 0</c> there, a second, independent trigger beside the reference's own documented defect
/// (<see cref="FixtureCases.DefectiveStationsOf"/>, evaluated on the reference's composition, not the tree's own). A
/// future fixture, or a future change to the transport set, could start eliminating a trace species on the tree's
/// own solve without the reference agreeing, and the wide "either trigger skips it" rule would then hide the
/// disagreement instead of surfacing it. This fact pins today's list of such stations, over every rocket fixture
/// with transport, to what it is today: empty. A future non-empty list turns the fact red and is looked at.
/// </summary>
[Collection("solver")]
public sealed class ReactingFieldsPinningTests
{
    /// <summary>No station of the tree's own end-to-end rocket solve reports a trace elimination today.</summary>
    [Fact]
    public void NoEndToEndRocketStationEliminatesATraceSpeciesToday()
    {
        var eliminating = StationsWithATraceEliminationOnTheTreesOwnSolve();
        Assert.Equal([], eliminating);
    }

    /// <summary>
    /// Every "fixtureName[stationName]" of a rocket-with-transport fixture whose tree-solved station reports
    /// <see cref="APThermo.Transport.TransportFigures.TraceEliminations"/> above zero.
    /// </summary>
    private static List<string> StationsWithATraceEliminationOnTheTreesOwnSolve()
    {
        var eliminating = new List<string>();
        foreach (var path in FixtureFiles.Enumerate("rocket"))
        {
            var name = Path.GetFileNameWithoutExtension(path)!;
            var c = FixtureCases.Load("rocket", name);
            if (!c.Inputs.GetProperty("transport").GetBoolean())
            {
                continue;
            }

            var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, c);
            var problem = FixtureCases.RocketProblemOf(c);
            var result = SolverFixture.Shared.Solver.Solve(propellant, problem);
            Assert.Equal(CaseStatus.Ok, result.Status);
            foreach (var station in result.Stations)
            {
                if (station.Transport is { TraceEliminations: > 0 })
                {
                    eliminating.Add($"{name}[{station.Name}]");
                }
            }
        }

        return eliminating;
    }
}
