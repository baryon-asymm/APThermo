using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// The "Shape check" named-construction rule, read from syntax: every creation of a type whose constructor has a declared
/// `parameters` exception in a node's `## Shape exceptions` table names all its arguments. Asserts nothing: <c>ShapeTests</c>
/// compares the creations <see cref="Creations"/> reports, by their <c>AllArgumentsNamed</c> flag, with the limit.
/// </summary>
internal static class NamedConstruction
{
    /// <summary>The file's own namespace and imports, and the node whose source it is: what a written name is resolved
    /// against ("Shape check", named construction). `NamedConstruction`'s own vocabulary, private to the resolution below.</summary>
    private readonly record struct FileScope(string Namespace, IReadOnlySet<string> Usings, Node Node);

    /// <summary>Every type a node's `## Shape exceptions` table declares a `parameters` row for on its own constructor
    /// (`Type.Type` in the row's `Where`, or `Outer.Inner.Inner` for a nested type: the last two dotted segments equal): the
    /// candidates <see cref="Creations"/> resolves a written name against.</summary>
    public static IReadOnlyCollection<WideConstructorType> Candidates() => Tree.Nodes.Where(node => node.AssemblyName is not null)
        .SelectMany(node => NodeDocuments.ShapeExceptions(node).Where(exception => exception.Rule == "parameters" && IsConstructorPattern(exception.Where))
            .Select(exception => new WideConstructorType(node, exception.Where.Split('.')[^1])))
        .ToList();

    private static bool IsConstructorPattern(string where)
    {
        var segments = where.Split('.');
        return segments.Length >= 2 && segments[^1] == segments[^2];
    }

    /// <summary>
    /// Every creation, anywhere in the tree, whose written name resolves to one of <paramref name="candidates"/>
    /// ("Shape check", named construction): an explicit <c>new T(…)</c>, or a target-typed <c>new(…)</c> initialising a
    /// variable, field or property declared with a resolving name; whether every argument of the creation is named. A simple
    /// name resolves to a candidate when the file's namespace is the candidate's, or lies inside it, or the file imports it
    /// (a global <c>using</c> included), and the file's own node declares no other type of the same simple name; a qualified
    /// name resolves when its qualifier is the candidate's namespace. Any other target-typed creation, and any name that
    /// resolves to none of the candidates, is left to review and reports nothing.
    /// </summary>
    public static IEnumerable<(Node Node, string TypeName, string File, int Line, bool AllArgumentsNamed)> Creations(
        IReadOnlyCollection<WideConstructorType> candidates)
    {
        var bySimpleName = candidates.ToLookup(type => type.SimpleName, StringComparer.Ordinal);
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
