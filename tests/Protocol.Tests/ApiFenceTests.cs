using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The grammar <see cref="ApiDeclarations"/> reads, on documents written here rather than read from the tree, so each
/// rule the tree facts rely on has been seen to hold, and to fail, on a known input (AGENTS.md §13).
/// </summary>
public sealed class ApiFenceTests
{
    private const string Fence = "```";

    /// <summary>The first info word selects the language, independent of marker, length, case and up to three leading spaces.</summary>
    [Theory]
    [InlineData("```C#", "```")]
    [InlineData(" ```CSharp", "  ```")]
    [InlineData("  ~~~~ Cs linenos", " ~~~~~")]
    [InlineData("~~~csharp,linenos", "~~~")]
    [InlineData("   `````\tcsharp extra", "   ``````\t")]
    public void RecognizesCSharpFences(string opening, string closing)
    {
        System.ArgumentNullException.ThrowIfNull(opening);
        var indent = opening.Length - opening.TrimStart().Length;
        var section = Assert.Single(ApiDeclarations.ImplementedSections("## Done ✅\n" + opening + "\n" +
            new string(' ', indent) + "class Seen {}\n" + closing + "\nprose"));
        Assert.Equal(("class Seen {}", false, 3), section);
    }

    /// <summary>Only the matching closing delimiter ends a block; marks and nested examples in another language are literal.</summary>
    [Fact]
    public void ForeignFencesIsolateMarksAndExampleDeclarations()
    {
        var document = string.Join('\n', "## Done ✅", "````text", "## " + "(tree contract)" + " ⏳",
            "```csharp", "class Ghost {}", "```", "~~~~", "````` trailing text", "````", "~~~cs", "class Seen {}");
        var section = Assert.Single(ApiDeclarations.ImplementedSections(document));
        Assert.Equal(("class Seen {}", false, 11), section);
        Assert.Equal(("Seen", false), Assert.Single(ApiDeclarations.DeclaredTypeSections(document)));
    }

    /// <summary>An unlabelled API signature is reported at its fence even beside a correctly labelled declaration.</summary>
    [Theory]
    [InlineData("public static class Converter { public static int Twice(string quantity); }")]
    [InlineData("public static int Twice(string quantity);")]
    [InlineData("internal readonly record struct Quantity(double Value);")]
    public void UnlabelledApiDeclarationsNameTheirOpeningLine(string declaration)
    {
        var document = string.Join('\n', "## Done ✅", Fence + "cs", "public class Other {}", Fence, Fence, declaration, Fence);
        Assert.Equal(5, Assert.Single(ApiDeclarations.UnlabelledDeclarationLines(document)));
    }

    /// <summary>Commands, prose, scripts, labels for other languages and future work may contain ordinary examples.</summary>
    [Fact]
    public void OrdinaryExamplesDoNotRequireACSharpLabel()
    {
        var document = string.Join('\n', "## Done ✅", Fence, "dotnet test tests/Project.Tests", Fence,
            Fence, "class Example { constructor(value) { this.value = value; } }", Fence,
            Fence, "interface Line { name: string; }", Fence, Fence, "class Item:\n    pass", Fence,
            Fence, "public transport on Saturdays", Fence,
            Fence + "text", "public class Printed {}", Fence,
            "````text", Fence, "public class Nested {}", Fence, "````",
            "## Planned ⏳", Fence, "public class Future {}", Fence);
        Assert.Empty(ApiDeclarations.UnlabelledDeclarationLines(document));
    }

    /// <summary>The nearest mark above a block decides it; a mark-like character inside the code does not; a document
    /// without marks is ✅ throughout.</summary>
    [Fact]
    public void TheNearestMarkAboveABlockDecidesIt()
    {
        var document = string.Join('\n',
            "## Done ✅", Fence + "csharp", "public sealed class Done; // ⏳ in a comment", Fence,
            "## Planned ⏳", Fence + "csharp", "public sealed class Planned;", Fence);
        var blocks = ApiDeclarations.ImplementedCsharpBlocks(document).ToList();
        var single = Assert.Single(blocks);
        Assert.Contains("Done", single, System.StringComparison.Ordinal);
        _ = Assert.Single(ApiDeclarations.ImplementedCsharpBlocks(Fence + "csharp\npublic class Unmarked;\n" + Fence));
        Assert.True(ApiDeclarations.NamesType(document, "Done"));
        Assert.False(ApiDeclarations.NamesType(document, "Planned"));
    }

    /// <summary>A record closed by a semicolon owns its positional parameters and nothing after it; a one-line enum owns
    /// nothing after it; a type whose brace opens on the next line owns what follows.</summary>
    [Fact]
    public void ABodilessTypeDoesNotSwallowTheMembersAfterIt()
    {
        var block = string.Join('\n',
            "public static class Outer",
            "{",
            "    public readonly record struct Pair(int Left, int Right);",
            "    public enum Side { Left, Right }",
            "    public static int Sum(Pair pair);",
            "}");
        var outer = Assert.Single(SyntaxFactory.ParseCompilationUnit(block).Members.OfType<ClassDeclarationSyntax>());
        var pair = Assert.Single(outer.Members.OfType<RecordDeclarationSyntax>());
        Assert.Equal(["Left", "Right"], pair.ParameterList!.Parameters.Select(parameter => parameter.Identifier.ValueText));
        Assert.Equal("Side", Assert.Single(outer.Members.OfType<EnumDeclarationSyntax>()).Identifier.ValueText);
        Assert.Equal("Sum", Assert.Single(outer.Members.OfType<MethodDeclarationSyntax>()).Identifier.ValueText);
    }

    /// <summary>A method returning a tuple is read by its own name, not as a member named after a modifier.</summary>
    [Fact]
    public void AModifierBeforeATupleIsNotAMemberName()
    {
        var declarations = ParsedMethodNames("internal static (long Low, long High) Band(int count);");
        Assert.DoesNotContain("static", declarations);
        Assert.Contains("Band", declarations);
    }

    /// <summary>A tuple return type or a parameter list split across lines is read by the method's own name, and nothing
    /// in its parameters is read as a declaration: the parameters are followed from the name's own parenthesis. The
    /// fourth and fifth cases (a tuple closing on the name's line, parameters below; the fifth with an attribute on the
    /// continuation line) were red before 2026-10-02: `p` was read as a field and `NotNullWhen` as a member (found by
    /// the CPM orchestrator, who ran both parsers side by side). The sixth, an attribute without a tuple, and `new`
    /// before a tuple (CPM's reverse case) were green before and stay as positive controls.</summary>
    [Theory]
    [InlineData("internal static (long Low,\n    long High) Band(int count);")]
    [InlineData("internal static (long Low,\n    long High)\n    Band(\n        int count);")]
    [InlineData("public static int Band(\n    int count,\n    int width);")]
    [InlineData("public static (int Low,\n    int High) Band(\n        int count,\n        double p = 0.5);")]
    [InlineData("public static (int Low,\n    int High) Band(int count,\n        int n, [NotNullWhen(true)] out string? why);")]
    [InlineData("public static bool Band(int count,\n    int n, [NotNullWhen(true)] out string? why);")]
    [InlineData("public new (int A, int B) Band(int count);")]
    public void ADeclarationSplitAcrossLinesIsReadByItsMemberName(string declaration)
    {
        var names = ParsedMethodNames(declaration + "\npublic static int After();");
        Assert.Equal(["Band", "After"], names);
    }

    /// <summary>A section is a tree contract only when its own heading carries the marker; the next heading resets it.</summary>
    [Fact]
    public void OnlyAMarkedHeadingOpensATreeContract()
    {
        var document = string.Join('\n',
            "## Friends " + "(tree contract)" + " ✅", Fence + "csharp", "internal sealed class Shared;", Fence,
            "## Public ✅", Fence + "csharp", "public sealed class Open;", Fence);
        var sections = ApiDeclarations.DeclaredTypeSections(document).ToList();
        Assert.Contains(("Shared", true), sections);
        Assert.Contains(("Open", false), sections);
        Assert.True(ApiDeclarations.NamesTypeInTreeContract(document, "Shared"));
        Assert.False(ApiDeclarations.NamesTypeInPackageSurface(document, "Shared"));
    }

    private static string[] ParsedMethodNames(string members) =>
        [.. SyntaxFactory.ParseCompilationUnit("class Example { " + members + " }").DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Select(method => method.Identifier.ValueText)];
}
