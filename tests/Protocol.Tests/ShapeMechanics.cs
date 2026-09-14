using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// The two "Shape check" rules with no numeric limit, read from syntax: mechanics (no <c>partial</c> type outside the
/// <c>[GeneratedRegex]</c> exception, no <c>#region</c>, no type named <c>*Helper(s)</c>/<c>*Util(s)</c>/<c>*Common</c>) and
/// named construction (every creation of a type with a declared parameters exception names its arguments). Asserts nothing:
/// both report occurrences, for <c>ShapeTests</c> to compare with a node's <c>## Shape exceptions</c> table.
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

    /// <summary>
    /// Every creation, anywhere in the tree, of a type named in <paramref name="wideConstructorTypes"/> (the simple names of
    /// every node's declared parameters exceptions): a <c>new T(…)</c> naming the type by its simple name, or a target-typed
    /// <c>new(…)</c> initialising a variable, field or property declared with that name; whether every argument is named.
    /// Any other target-typed creation (a return, an argument, a more complex target) is not resolved and reports nothing,
    /// as the rule leaves it to review.
    /// </summary>
    public static IEnumerable<(string TypeName, string File, int Line, bool AllArgumentsNamed)> Constructions(IReadOnlySet<string> wideConstructorTypes)
    {
        foreach (var node in Tree.Nodes.Where(n => n.AssemblyName is not null))
        {
            foreach (var (path, tree) in SourceSyntax.Trees(node))
            {
                foreach (var found in ConstructionsIn(path, tree, wideConstructorTypes))
                {
                    yield return found;
                }
            }
        }
    }

    private static IEnumerable<(string TypeName, string File, int Line, bool AllArgumentsNamed)> ConstructionsIn(
        string path, SyntaxTree tree, IReadOnlySet<string> wideConstructorTypes)
    {
        foreach (var creation in tree.GetRoot().DescendantNodes().Where(IsCreation))
        {
            if (ConstructedTypeName(creation) is not { } typeName || !wideConstructorTypes.Contains(typeName))
            {
                continue;
            }

            var line = tree.GetLineSpan(creation.Span).StartLinePosition.Line;
            yield return (typeName, path, line + 1, ArgumentsOf(creation).All(argument => argument.NameColon is not null));
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

    private static bool IsCreation(SyntaxNode node) => node is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax;

    private static string? ConstructedTypeName(SyntaxNode creation) => creation switch
    {
        ObjectCreationExpressionSyntax oce => SimpleTypeName(oce.Type),
        ImplicitObjectCreationExpressionSyntax ioce => DeclaredTypeName(ioce),
        _ => null,
    };

    private static SeparatedSyntaxList<ArgumentSyntax> ArgumentsOf(SyntaxNode creation) => creation switch
    {
        ObjectCreationExpressionSyntax oce => oce.ArgumentList?.Arguments ?? default,
        ImplicitObjectCreationExpressionSyntax ioce => ioce.ArgumentList?.Arguments ?? default,
        _ => default,
    };

    /// <summary>The type a target-typed <c>new(…)</c> initialises, when it is the initialiser of a variable, field or
    /// property declared with an explicit type; anything else (a return, an argument, …) is left unresolved on purpose.</summary>
    private static string? DeclaredTypeName(ImplicitObjectCreationExpressionSyntax creation) => creation.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } =>
            SimpleTypeName(declaration.Type),
        EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => SimpleTypeName(property.Type),
        _ => null,
    };

    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        _ => type.ToString(),
    };
}
