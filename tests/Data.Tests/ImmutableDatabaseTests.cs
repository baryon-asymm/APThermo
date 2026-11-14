namespace APThermo.Data.Tests;

/// <summary>The loaded object graph is read-only rather than merely typed as read-only.</summary>
public sealed class ImmutableDatabaseTests
{
    private static void AssertReadOnly<T>(IReadOnlyList<T> values)
    {
        Assert.NotEmpty(values);
        if (values is IList<T> list)
        {
            _ = Assert.Throws<NotSupportedException>(() => list[0] = values[0]);
        }
    }

    /// <summary>Every published collection tier prevents mutation through IList when it exposes that interface.</summary>
    [Fact]
    public void LoadedCollectionsAndNestedRecordsAreImmutable()
    {
        var database = SpeciesDatabase.LoadBundled();
        AssertReadOnly(database.Products);
        AssertReadOnly(database.Reactants);
        AssertReadOnly(database.Records("O2"));
        AssertReadOnly(database.Provenance.DefaultIntervalBounds);
        var species = database["O2"];
        AssertReadOnly(species.Formula);
        AssertReadOnly(species.Intervals);
        AssertReadOnly(species.Intervals[0].Exponents);
        AssertReadOnly(species.Intervals[0].Coefficients);
        AssertReadOnly(database.Transport!.Entries);
        var entry = database.Transport.Entries.First(e => e.Viscosity.Count > 0 && e.Conductivity.Count > 0);
        AssertReadOnly(entry.Viscosity);
        AssertReadOnly(entry.Conductivity);
    }
}
