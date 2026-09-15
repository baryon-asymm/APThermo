using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The size, nesting and parameter measurements of the Shape level (`tests/Protocol.Tests/BOOT.md`, "Shape check"), read from
/// the C# syntax trees of a node's own source files (<see cref="SourceSyntax"/>). Asserts nothing: every method below reports a
/// figure at a place, and comparing it with a limit or a node's declared exception is <c>ShapeTests</c>' concern, not this one's.
/// </summary>
internal static class ShapeMeasures
{
    /// <summary>Every type or delegate, top-level and nested, the count of its own span's lines that hold code (a nested
    /// type counts inside its outer type's count and on its own, "Shape check", type lines).</summary>
    public static IEnumerable<(string Where, string File, int Line, int Lines)> TypeLines(Node node)
    {
        foreach (var (path, tree) in SourceSyntax.Trees(node))
        {
            foreach (var declaration in tree.GetRoot().DescendantNodes().Where(IsTypeLike))
            {
                yield return (QualifiedTypeName(declaration), path, StartLine(tree, declaration), CodeLines(tree, declaration));
            }
        }
    }

    /// <summary>Every method, constructor, operator, conversion operator, accessor with a body, and local function; the
    /// count of its own span's lines that hold code ("Shape check", method lines). A record's primary constructor has no
    /// block to span and is not a method; <see cref="Parameters"/> counts its parameters separately.</summary>
    public static IEnumerable<(string Where, string File, int Line, int Lines)> MethodLines(Node node)
    {
        foreach (var (path, tree) in SourceSyntax.Trees(node))
        {
            foreach (var member in tree.GetRoot().DescendantNodes().Where(IsMeasuredMember))
            {
                yield return (MemberName(member), path, StartLine(tree, member), CodeLines(tree, member));
            }
        }
    }

    /// <summary>
    /// Every measured member's deepest nesting of <c>if</c> (an <c>else if</c> continuing its chain rather than adding a level),
    /// <c>for</c>, <c>foreach</c>, <c>while</c>, <c>do</c>, <c>switch</c> and <c>try</c> ("Shape check", nesting). A lambda or a
    /// local function does not reset the count: a depth is attributed to every measured member that contains the statement,
    /// so a local function's own figure already carries the depth of where it is declared, and the member it is declared in
    /// sees the same depth through it — extracting a nested block into a local function does not lower either figure.
    /// </summary>
    public static IEnumerable<(string Where, string File, int Line, int Depth)> Nesting(Node node)
    {
        foreach (var (path, tree) in SourceSyntax.Trees(node))
        {
            var root = tree.GetRoot();
            var members = root.DescendantNodes().Where(IsMeasuredMember).ToHashSet();
            var depths = members.ToDictionary(member => member, _ => 0);
            foreach (var statement in root.DescendantNodes().Where(ContributesDepth))
            {
                Deepen(statement, depths, members);
            }

            foreach (var member in members)
            {
                yield return (MemberName(member), path, StartLine(tree, member), depths[member]);
            }
        }
    }

    /// <summary>Every method, constructor (a record's primary constructor included), local function and delegate; its declared
    /// parameter count ("Shape check", parameters; lambdas are never counted, and never measured here at all).</summary>
    public static IEnumerable<(string Where, string File, int Line, int Count)> Parameters(Node node)
    {
        foreach (var (path, tree) in SourceSyntax.Trees(node))
        {
            var root = tree.GetRoot();
            foreach (var member in root.DescendantNodes().Where(IsParameterized))
            {
                yield return (MemberName(member), path, StartLine(tree, member), ParameterListOf(member)!.Parameters.Count);
            }

            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type.ParameterList is not { } primaryConstructor)
                {
                    continue;
                }

                yield return (QualifiedTypeName(type) + "." + type.Identifier.Text, path, StartLine(tree, type), primaryConstructor.Parameters.Count);
            }
        }
    }

    private static void Deepen(SyntaxNode statement, Dictionary<SyntaxNode, int> depths, HashSet<SyntaxNode> members)
    {
        var depth = statement.AncestorsAndSelf().Count(ContributesDepth);
        foreach (var container in statement.Ancestors())
        {
            if (members.Contains(container) && depths[container] < depth)
            {
                depths[container] = depth;
            }
        }
    }

    private static bool IsTypeLike(SyntaxNode node) => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;

    private static bool IsMeasuredMember(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax or ConstructorDeclarationSyntax or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax or LocalFunctionStatementSyntax => true,
        AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
        _ => false,
    };

    private static bool IsParameterized(SyntaxNode node) =>
        node is MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax or DelegateDeclarationSyntax;

    private static ParameterListSyntax? ParameterListOf(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax m => m.ParameterList,
        ConstructorDeclarationSyntax c => c.ParameterList,
        LocalFunctionStatementSyntax l => l.ParameterList,
        DelegateDeclarationSyntax d => d.ParameterList,
        _ => null,
    };

    private static bool ContributesDepth(SyntaxNode node) =>
        IsDepthTracked(node) && node is not IfStatementSyntax { Parent: ElseClauseSyntax };

    private static bool IsDepthTracked(SyntaxNode node) => node is IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax
        or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax or SwitchStatementSyntax or TryStatementSyntax;

    /// <summary>The type's or the member's own qualified name: <c>Type.Member</c>, a constructor's member name equal to the
    /// type's own ("Shape check", the exceptions table's <c>Where</c> form); a local function qualified by its enclosing type
    /// only, since no node names one as an exception.</summary>
    private static string MemberName(SyntaxNode member)
    {
        var enclosing = member.Ancestors().First(a => a is BaseTypeDeclarationSyntax);
        var name = member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax c => "operator " + c.Type,
            LocalFunctionStatementSyntax l => l.Identifier.Text,
            AccessorDeclarationSyntax a => a.Keyword.Text,
            _ => member.ToString(),
        };
        return QualifiedTypeName(enclosing) + "." + name;
    }

    /// <summary>The simple name of a type or delegate, dotted with its own enclosing types (<c>Outer.Inner</c>).</summary>
    private static string QualifiedTypeName(SyntaxNode typeOrDelegate)
    {
        var names = new List<string>();
        for (var current = typeOrDelegate; current is not null; current = current.Parent)
        {
            var name = current switch
            {
                BaseTypeDeclarationSyntax t => t.Identifier.Text,
                DelegateDeclarationSyntax d => d.Identifier.Text,
                _ => null,
            };
            if (name is not null)
            {
                names.Insert(0, name);
            }
        }

        return string.Join(".", names);
    }

    /// <summary>The two boundary tokens of a declaration's own span: its first token after any attribute lists (so that
    /// attributes and the documentation comment above, which is leading trivia, are excluded) and its very last token (the
    /// closing brace, or the semicolon of a bodyless or expression-bodied declaration). <see cref="StartLine"/> and
    /// <see cref="CodeLines"/> both walk from these same two tokens, so the boundary itself is computed once.</summary>
    private static (SyntaxToken First, SyntaxToken Last) BoundaryTokens(SyntaxNode node) => (FirstTokenAfterAttributes(node), node.GetLastToken());

    /// <summary>The 1-based line of a declaration's first boundary token (<see cref="BoundaryTokens"/>): the place
    /// reported for a type-lines, method-lines, parameters or nesting row ("Shape check").</summary>
    private static int StartLine(SyntaxTree tree, SyntaxNode node) => tree.GetLineSpan(BoundaryTokens(node).First.Span).StartLinePosition.Line + 1;

    /// <summary>
    /// The count of lines between the same boundary tokens (<see cref="BoundaryTokens"/>) that hold code ("Shape check",
    /// type lines, the 2026-09-14 exclusion): a line counts once any token of the declaration's own span falls on it,
    /// whatever comment trivia shares that line; a line of only white space or only comment (<c>//</c>, <c>///</c>, or a
    /// block comment's line) does not, because no token touches it. A token that itself spans several lines (a raw string
    /// literal) counts every line it occupies, since those lines hold that token's own text, not a comment.
    /// </summary>
    private static int CodeLines(SyntaxTree tree, SyntaxNode node)
    {
        var (first, last) = BoundaryTokens(node);
        var lines = new HashSet<int>();
        for (var token = first; ; token = token.GetNextToken())
        {
            var span = tree.GetLineSpan(token.Span);
            for (var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++)
            {
                lines.Add(line);
            }

            if (token == last)
            {
                return lines.Count;
            }
        }
    }

    private static SyntaxToken FirstTokenAfterAttributes(SyntaxNode node)
    {
        foreach (var child in node.ChildNodesAndTokens())
        {
            if (child.IsNode && child.AsNode() is AttributeListSyntax)
            {
                continue;
            }

            return child.IsToken ? child.AsToken() : child.AsNode()!.GetFirstToken();
        }

        return node.GetFirstToken();
    }
}
