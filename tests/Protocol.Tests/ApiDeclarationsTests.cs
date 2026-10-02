namespace APThermo.Protocol.Tests;

/// <summary>
/// The grammar of an API.md read by <see cref="ApiDeclarations"/>: how a declaration line is turned into the names it declares.
/// </summary>
public sealed class ApiDeclarationsTests
{
    /// <summary>A method whose return type is a tuple, with named or unnamed elements, is read by its own name and not as a
    /// member named after the modifier that precedes the tuple.</summary>
    [Theory]
    [InlineData("internal static (long Low, long High) Band(int count);")]
    [InlineData("internal static (long, long) Band(int count);")]
    [InlineData("public (Dictionary<string, int> Counts, long Total) Band(int count);")]
    public void ATupleReturningDeclarationIsReadByItsMemberName(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var names = ApiDeclarations.Declarations(line).Select(declaration => declaration.Name).ToList();

        Assert.Equal(["Band"], names);
    }
}
