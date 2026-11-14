namespace APThermo.Protocol.Tests;

/// <summary>Explicit written base contracts are checked; omitted base lists are deliberate partial descriptions.</summary>
public sealed class SignatureInheritanceTests
{
    /// <summary>Wrong class/interface promises cannot hide behind a matching owner name.</summary>
    [Theory]
    [InlineData("", true)]
    [InlineData(": object", true)]
    [InlineData(": IDisposable", false)]
    [InlineData(": Exception", false)]
    [InlineData(": IComparable<T>", false)]
    public void ExplicitClassBasesMustExist(string bases, bool matches) =>
        AssertMatch("internal sealed class SignatureExample<T> " + bases + " where T : class {}", matches);

    /// <summary>Generic record arguments/nullability and interfaces are promises independently of omitted members.</summary>
    [Theory]
    [InlineData("", true)]
    [InlineData(": SignatureBase<T>", true)]
    [InlineData(": APThermo.Protocol.Tests.SignatureBase<T>", true)]
    [InlineData(": IDisposable", true)]
    [InlineData(": SignatureBase<T>, IDisposable", true)]
    [InlineData(": SignatureBase<string>", false)]
    [InlineData(": SignatureBase<T?>", false)]
    [InlineData(": SignatureBase<T>, IComparable<T>", false)]
    [InlineData(": IEnumerable<T>", false)]
    public void RecordBaseContractsRetainTheirConstructedTypes(string bases, bool matches)
    {
        using var actual = new SignatureDerived<string>("control");
        AssertMatch("internal sealed record SignatureDerived<T> " + bases + " where T : class {}", matches);
    }

    /// <summary>Interface closure permits a real inherited promise without requiring all other inherited interfaces.</summary>
    [Theory]
    [InlineData("", true)]
    [InlineData(": IEnumerable<T>", true)]
    [InlineData(": System.Collections.IEnumerable", true)]
    [InlineData(": IEnumerable<string>", false)]
    [InlineData(": IDisposable", false)]
    public void WrittenInterfaceBasesMatchTheirCompiledClosure(string bases, bool matches) =>
        AssertMatch("internal interface ISignatureSequence<T> " + bases + " {}", matches);

    /// <summary>A record struct's real synthesized interface can be promised, but cannot acquire a class base.</summary>
    [Theory]
    [InlineData(": IEquatable<Instruction>", true)]
    [InlineData(": IEquatable<string>", false)]
    [InlineData(": IDisposable", false)]
    [InlineData(": ValueType", false)]
    public void WrittenStructBasesMatchTheirActualKind(string bases, bool matches) =>
        AssertMatch("internal readonly record struct Instruction " + bases + " {}", matches);

    private static void AssertMatch(string block, bool expected)
    {
        var problems = Declaration.Problems(block, "APThermo.Protocol.Tests", "fixture/API.md", 1).ToArray();
        Assert.Equal(expected, problems.Length == 0);
        if (!expected)
        {
            Assert.All(problems, problem => Assert.StartsWith("fixture/API.md:", problem, StringComparison.Ordinal));
        }
    }
}
