namespace APThermo.Thermo.Tests;

/// <summary>
/// Bits level: the table the builder produces for every fixture case gives the recorded bits. A tripwire over the
/// builder's output, the way the surface snapshot is one over the contract (AGENTS.md §13): a decomposition, a rename
/// or a reordering of code moves no line of <c>Bits.approved.txt</c>, and a line that does move is legitimate only
/// with the change of the builder's output named in the same commit.
/// </summary>
public sealed class BitSnapshotTests
{
    private static readonly BitSnapshot Snapshot = new();

    /// <summary>One theory case per fixture case file; the list comes from the fixtures node, not from this test.</summary>
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var path in BitSnapshot.CaseFiles())
        {
            data.Add(BitSnapshot.Relative(path));
        }

        return data;
    }

    /// <summary>Every fixture case gives the recorded bits.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "BitSnapshot")]
    public void EveryFixtureCaseGivesTheRecordedBits(string fixtureCase)
    {
        var problem = Snapshot.Problem(fixtureCase);
        Assert.True(problem is null, problem);
    }

    /// <summary>The reverse of the theory above: an approved line whose fixture was deleted or renamed, which a theory has no case for and so cannot fail on, fails this fact instead.</summary>
    [Fact]
    [Trait("Category", "BitSnapshot")]
    public void EveryRecordedLineIsAFixtureCase()
    {
        var stale = Snapshot.StaleKeys();
        Assert.True(stale.Count == 0, $"{stale.Count} line(s) of Bits.approved.txt name no enumerated fixture case:\n" + string.Join("\n", stale));
    }
}
