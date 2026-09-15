using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// The "Shape check" mechanics rule, read from syntax: no <c>partial</c> type outside the <c>[GeneratedRegex]</c> exception,
/// no <c>#region</c>, no type whose name ends in <c>Helper</c>, <c>Helpers</c>, <c>Util</c>, <c>Utils</c> or <c>Common</c>.
/// Asserts nothing: reports occurrences, for <c>ShapeTests</c> to compare with a node's <c>## Shape exceptions</c> table.
/// </summary>
internal static class ShapeMechanics
{
    private static readonly string[] BannedSuffixes = ["Helper", "Helpers", "Util", "Utils", "Common"];

    /// <summary>Every occurrence the mechanics rule counts against a node: a partial type declaration with no
    /// <c>[GeneratedRegex]</c> member anywhere in the type, a type named with a banned suffix, or a file with a
    /// <c>#region</c> directive.</summary>
    public static IEnumerable<(string Where, string File, int Line, string Problem)> Findings(Node node)
    {
        foreach (var (path, tree) in SourceSyntax.Trees(node))
        {
            var root = tree.GetRoot();
            foreach (var finding in TypeFindings(tree, root))
            {
                yield return finding;
            }

            if (root.DescendantTrivia().Any(trivia => trivia.IsKind(SyntaxKind.RegionDirectiveTrivia)))
            {
                yield return (Tree.Relative(path), path, 1, "contains a #region");
            }
        }
    }

    private static IEnumerable<(string Where, string File, int Line, string Problem)> TypeFindings(SyntaxTree tree, SyntaxNode root)
    {
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var line = tree.GetLineSpan(type.Identifier.Span).StartLinePosition.Line;
            var where = type.Identifier.Text;
            if (type.Modifiers.Any(SyntaxKind.PartialKeyword) && !HasGeneratedRegexMember(type))
            {
                yield return (where, tree.FilePath, line + 1, "partial type without a [GeneratedRegex] member");
            }

            if (BannedSuffixes.Any(suffix => where.EndsWith(suffix, StringComparison.Ordinal)))
            {
                yield return (where, tree.FilePath, line + 1, $"type name ends in the banned suffix implied by '{where}'");
            }
        }
    }

    private static bool HasGeneratedRegexMember(TypeDeclarationSyntax type) => type.Members.OfType<MethodDeclarationSyntax>()
        .Any(method => method.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name.ToString().Contains("GeneratedRegex", StringComparison.Ordinal)));
}
