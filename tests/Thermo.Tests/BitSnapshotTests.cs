namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>
/// Bits level: the table the builder produces for every fixture case gives the recorded bits. A tripwire over the
/// builder's output, the way the surface snapshot is one over the contract (AGENTS.md §13): a decomposition, a rename
/// or a reordering of code moves no line of <c>Bits.approved.txt</c>, and a line that does move is legitimate only
/// with the change of the builder's output named in the same commit.
/// </summary>
public sealed class BitSnapshotTests : IClassFixture<BitSnapshot>
{
    private readonly BitSnapshot _snapshot;

    public BitSnapshotTests(BitSnapshot snapshot) => _snapshot = snapshot;

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

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_fixture_case_gives_the_recorded_bits(string fixtureCase)
    {
        if (_snapshot.NoApprovedFile)
        {
            Assert.Fail(
                $"No approved bit snapshot of the fixture cases' tables existed, so one was written to {BitSnapshot.ApprovedPath}. " +
                $"Read it, satisfy yourself that it is what the builder produces today, commit it, and re-run. Case: {fixtureCase}.");
        }

        var recorded = _snapshot.Recorded(fixtureCase);
        if (recorded is null)
        {
            Assert.Fail(
                $"{fixtureCase} has no line in {BitSnapshot.ApprovedPath}. What the tables are now was written to " +
                $"{BitSnapshot.ActualPath}; if the case is new, replace the approved file with it in the same commit.");
        }

        var current = _snapshot.Current(fixtureCase);
        Assert.True(
            recorded == current,
            $"The table of {fixtureCase} is now {current}, the snapshot records {recorded}. A decomposition, a rename or a " +
            $"reordering of code must move no line here (BOOT.md, the Bits level). If the builder's output really changed, " +
            $"name the change in the commit and replace {BitSnapshot.ApprovedPath} with {BitSnapshot.ActualPath}.");
    }
}
