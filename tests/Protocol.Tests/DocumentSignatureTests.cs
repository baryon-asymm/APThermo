using System.Collections.Immutable;

namespace APThermo.Protocol.Tests;

/// <summary>Semantic controls for the documented signature against the built owner.</summary>
public sealed class DocumentSignatureTests
{
    /// <summary>Each change must fail independently while real overloads remain accepted.</summary>
    [Theory]
    [InlineData("public static Node? NodeOf(Type type);", true)]
    [InlineData("public static Node? NodeOf(Assembly assembly);", true)]
    [InlineData("public static Node? NodeOf(short type);", false)]
    [InlineData("public static Node? NodeOf(Type argument);", false)]
    [InlineData("public static Node NodeOf(Type type);", false)]
    [InlineData("public static int NodeOf(Type type);", false)]
    [InlineData("internal static Node? NodeOf(Type type);", false)]
    [InlineData("public Node? NodeOf(Type type);", false)]
    public void MatchesOneCompiledOverload(string member, bool expected) =>
        AssertMatch("internal static class NodeAssemblies { " + member + " }", expected);

    /// <summary>External aliases, ref kinds and optional values describe the same callable overload.</summary>
    [Theory]
    [InlineData("TextWriter", "out", " = true", true)]
    [InlineData("System.IO.TextWriter", "out", " = true", true)]
    [InlineData("TextWriter", "ref", " = true", false)]
    [InlineData("TextWriter", "out", " = false", false)]
    [InlineData("TextWriter", "out", "", false)]
    [InlineData("StringWriter", "out", " = true", false)]
    public void ChecksExternalTypesByReferenceAndDefault(string writer, string modifier, string optional, bool matches)
    {
        TryWrite(TextWriter.Null, [5], out var count);
        Assert.Equal(1, count);
        AssertMatch("public sealed class DocumentSignatureTests { " +
            $"private static void TryWrite({writer} writer, ImmutableArray<int> values, {modifier} int count, bool emit{optional});" + " }", matches);
    }

    /// <summary>Type shape, record kind and nested ownership are promises too.</summary>
    [Theory]
    [InlineData("internal sealed record Node", true)]
    [InlineData("internal sealed class Node", false)]
    [InlineData("internal record Node", false)]
    [InlineData("public sealed record Node", false)]
    [InlineData("internal static class NodeAssemblies { public sealed record Node {} }", false)]
    public void ChecksTypeKindAndOwner(string header, bool matches)
    {
        ArgumentNullException.ThrowIfNull(header);
        AssertMatch(header.Contains('{', StringComparison.Ordinal) ? header : header + " {}", matches);
    }

    /// <summary>Properties named like another declared type cannot be mistaken for constructors.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameNamedMissingMemberIsReportedInEitherOrder(bool typeFirst)
    {
        const string owner = "internal static class NodeAssemblies { public static Node Node { get; } }";
        const string node = "internal sealed record Node {}";
        AssertMatch(typeFirst ? node + owner : owner + node, false);
    }

    /// <summary>Real constructors and positional properties are independently matched.</summary>
    [Theory]
    [InlineData("internal readonly record struct Instruction(OpCode Code, MemberInfo? Operand);", true)]
    [InlineData("internal readonly record struct Instruction(OpCode Code, MemberInfo Operand);", false)]
    [InlineData("internal readonly record struct Instruction(OpCode Code, MemberInfo? Value);", false)]
    [InlineData("internal readonly record struct Instruction { public Instruction(OpCode Code, MemberInfo? Operand); public OpCode Code { get; init; } }", true)]
    [InlineData("internal readonly record struct Instruction { public OpCode Code { get; set; } }", false)]
    [InlineData("internal readonly record struct Instruction { public OpCode Code { get; } }", false)]
    public void ChecksRecordConstructorAndAccessor(string block, bool matches) => AssertMatch(block, matches);

    /// <summary>Malformed or ownerless declarations report the original Markdown location.</summary>
    [Theory]
    [InlineData("public class Broken { public void Missing( ; }")]
    [InlineData("Console.WriteLine(1);")]
    public void UnsupportedSyntaxReportsDocumentLocation(string block) =>
        Assert.StartsWith("fixture/API.md:80:", Problems(block).First(), StringComparison.Ordinal);

    /// <summary>Multiline tuple/generic types resolve to the compiled semantic signature.</summary>
    [Fact]
    public void MultilineInternalGenericTupleMatches() =>
        AssertMatch("internal static class ApiDeclarations { public static IEnumerable<(string Name, bool TreeContract)>\n" +
            "DeclaredTypeSections(System.String document); }", true);

    /// <summary>Generic constraints, events, constants and accessor visibility are matched separately.</summary>
    [Theory]
    [InlineData("public const int Limit = 7;", true)]
    [InlineData("public const int Limit = 8;", false)]
    [InlineData("public static int Limit;", false)]
    [InlineData("public T? Value { get; }", true)]
    [InlineData("public T? Value { get; set; }", false)]
    [InlineData("public event Action? Changed;", true)]
    [InlineData("public event Action Changed;", false)]
    [InlineData("public event Action<int>? Changed;", false)]
    [InlineData("public TResult Convert<TResult>(T value, out bool success, params int[] counts) where TResult : struct;", true)]
    [InlineData("public TResult Convert<TResult>(T value, out bool success, int[] counts) where TResult : struct;", false)]
    [InlineData("public TResult Convert<TResult>(T value, out bool success, params int[] counts) where TResult : class;", false)]
    public void ChecksGenericMembers(string member, bool matches) =>
        AssertMatch("internal sealed class SignatureExample<T> where T : class { " + member + " }", matches);

    /// <summary>A matching generic arity alone cannot excuse incorrect type constraints or type parameter names.</summary>
    [Theory]
    [InlineData("<T> where T : class", true)]
    [InlineData("<T> where T : struct", false)]
    [InlineData("<T>", false)]
    [InlineData("<U> where U : class", false)]
    public void ChecksGenericTypeConstraints(string parameters, bool matches)
    {
        var example = new SignatureExample<string>();
        _ = example.Convert<int>("control", out _);
        AssertMatch("internal sealed class SignatureExample" + parameters + " {}", matches);
    }

    private static void TryWrite(TextWriter writer, ImmutableArray<int> values, out int count, bool emit = true)
    {
        count = values.Length;
        if (emit)
        {
            writer.Write(count);
        }
    }

    private static void AssertMatch(string block, bool expected)
    {
        var problems = Problems(block);
        if (expected)
        {
            Assert.Empty(problems);
        }
        else
        {
            Assert.NotEmpty(problems);
            Assert.All(problems, problem => Assert.StartsWith("fixture/API.md:", problem, StringComparison.Ordinal));
        }
    }

    private static string[] Problems(string block) =>
        [.. Declaration.Problems("using System.Reflection.Emit;\n" + block, "APThermo.Protocol.Tests", "fixture/API.md", 79)];
}
