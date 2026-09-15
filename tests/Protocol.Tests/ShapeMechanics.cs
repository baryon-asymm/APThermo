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

    /// <summary>The file's own namespace and imports, and the node whose source it is: what a written name is resolved
    /// against ("Shape check", named construction). `ShapeMechanics`' own vocabulary, private to the resolution below.</summary>
    private readonly record struct FileScope(string Namespace, IReadOnlySet<string> Usings, Node Node);

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
    /// Every creation, anywhere in the tree, whose written name resolves to one of <paramref name="wideConstructorTypes"/>
    /// ("Shape check", named construction): an explicit <c>new T(…)</c>, or a target-typed <c>new(…)</c> initialising a
    /// variable, field or property declared with a resolving name; whether every argument of the creation is named. A simple
    /// name resolves to a candidate when the file's namespace is the candidate's, or lies inside it, or the file imports it
    /// (a global <c>using</c> included), and the file's own node declares no other type of the same simple name; a qualified
    /// name resolves when its qualifier is the candidate's namespace. Any other target-typed creation, and any name that
    /// resolves to none of the candidates, is left to review and reports nothing.
    /// </summary>
    public static IEnumerable<(Node Node, string TypeName, string File, int Line, bool AllArgumentsNamed)> Constructions(
        IReadOnlyCollection<WideConstructorType> wideConstructorTypes)
    {
        var bySimpleName = wideConstructorTypes.ToLookup(type => type.SimpleName, StringComparer.Ordinal);
        var declaredNames = Tree.Nodes.Where(node => node.AssemblyName is not null).ToDictionary(node => node, DeclaredTypeNames);
        foreach (var node in Tree.Nodes.Where(node => node.AssemblyName is not null))
        {
            foreach (var (path, tree) in SourceSyntax.Trees(node))
            {
                foreach (var found in ConstructionsIn(node, path, tree, bySimpleName, declaredNames))
                {
                    yield return found;
                }
            }
        }
    }

    private static IEnumerable<(Node Node, string TypeName, string File, int Line, bool AllArgumentsNamed)> ConstructionsIn(
        Node node, string path, SyntaxTree tree, ILookup<string, WideConstructorType> bySimpleName,
        IReadOnlyDictionary<Node, IReadOnlySet<string>> declaredNames)
    {
        var root = tree.GetRoot();
        var scope = Scope(root, node);
        foreach (var creation in root.DescendantNodes().Where(IsCreation))
        {
            if (WrittenName(creation) is not { } written || !bySimpleName.Contains(written.SimpleName))
            {
                continue;
            }

            var candidate = bySimpleName[written.SimpleName].FirstOrDefault(type => Resolves(written, scope, type, declaredNames));
            if (candidate.Node is null)
            {
                continue;
            }

            var line = tree.GetLineSpan(creation.Span).StartLinePosition.Line;
            yield return (candidate.Node, candidate.SimpleName, path, line + 1, ArgumentsOf(creation).All(argument => argument.NameColon is not null));
        }
    }

    /// <summary>The file's own namespace (file-scoped or block form, either way) and its plain, non-aliased <c>using</c>
    /// imports, global ones included.</summary>
    private static FileScope Scope(SyntaxNode root, Node node)
    {
        var declaration = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Where(u => u.Alias is null && u.StaticKeyword.IsKind(SyntaxKind.None) && u.Name is not null)
            .Select(u => u.Name!.ToString())
            .ToHashSet(StringComparer.Ordinal);
        return new FileScope(declaration?.Name.ToString() ?? string.Empty, usings, node);
    }

    /// <summary>Whether the written name resolves to this candidate: a qualified name only through its qualifier text, a
    /// simple name through the file's namespace, its imports and its own node's declarations ("Shape check", named
    /// construction, the two resolution routes).</summary>
    private static bool Resolves(
        (string? Qualifier, string SimpleName) written, FileScope scope, WideConstructorType candidate,
        IReadOnlyDictionary<Node, IReadOnlySet<string>> declaredNames)
    {
        var candidateNamespace = candidate.Node.AssemblyName!;
        if (written.Qualifier is { } qualifier)
        {
            return qualifier == candidateNamespace || scope.Usings.Any(u => u + "." + qualifier == candidateNamespace);
        }

        var inScope = scope.Namespace == candidateNamespace
            || scope.Namespace.StartsWith(candidateNamespace + ".", StringComparison.Ordinal)
            || scope.Usings.Contains(candidateNamespace);
        return inScope && (scope.Node == candidate.Node || !declaredNames[scope.Node].Contains(written.SimpleName));
    }

    /// <summary>The simple names of every type the node declares in its own source, top-level and nested alike: what "the
    /// file's own node declares no other type of that name" reads against.</summary>
    private static IReadOnlySet<string> DeclaredTypeNames(Node node) => SourceSyntax.Trees(node)
        .SelectMany(entry => entry.Tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        .Select(declaration => declaration.Identifier.Text)
        .ToHashSet(StringComparer.Ordinal);

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

    /// <summary>The written name of a creation: an explicit <c>new T(…)</c>'s own type name, or a target-typed <c>new(…)</c>'s
    /// name resolved through <see cref="DeclaredType"/>. Null when a target-typed creation's declared type cannot be found
    /// (a return, an argument, a more complex target): left to review, on purpose.</summary>
    private static (string? Qualifier, string SimpleName)? WrittenName(SyntaxNode creation) => creation switch
    {
        ObjectCreationExpressionSyntax oce => NameParts(oce.Type),
        ImplicitObjectCreationExpressionSyntax ioce when DeclaredType(ioce) is { } type => NameParts(type),
        _ => null,
    };

    /// <summary>A written type name split into its qualifier, when it has one, and its own simple name.</summary>
    private static (string? Qualifier, string SimpleName) NameParts(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax q => (q.Left.ToString(), q.Right.Identifier.Text),
        AliasQualifiedNameSyntax a => (a.Alias.Identifier.Text, a.Name.Identifier.Text),
        SimpleNameSyntax s => (null, s.Identifier.Text),
        _ => (null, type.ToString()),
    };

    /// <summary>The type a target-typed <c>new(…)</c> initialises, when it is the initialiser of a variable, field or
    /// property declared with an explicit type; anything else (a return, an argument, …) is left unresolved on purpose.</summary>
    private static TypeSyntax? DeclaredType(ImplicitObjectCreationExpressionSyntax creation) => creation.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } => declaration.Type,
        EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Type,
        _ => null,
    };

    private static SeparatedSyntaxList<ArgumentSyntax> ArgumentsOf(SyntaxNode creation) => creation switch
    {
        ObjectCreationExpressionSyntax oce => oce.ArgumentList?.Arguments ?? default,
        ImplicitObjectCreationExpressionSyntax ioce => ioce.ArgumentList?.Arguments ?? default,
        _ => default,
    };
}
