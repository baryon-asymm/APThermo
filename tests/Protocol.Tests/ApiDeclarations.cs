using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The grammar of an <c>API.md</c>: its ✅ C# blocks and the declarations in them, and the text that sits under ✅ more
/// broadly (AGENTS.md §7). The one meaning of "named in the API.md" for <see cref="DeclarationTests"/> (a declaration
/// exists, read from the ✅ C# blocks) and <see cref="CoverageTests"/> (every exported type is at least named, in prose
/// or in code, under a ✅ mark). A type need not be given a full declaration: one public only because a framework
/// requires it may be named in a sentence, and that still counts.
/// </summary>
internal static class ApiDeclarations
{

    private static (string Marker, string Info, int Indent)? FenceOf(string line)
    {
        var indent = 0;
        while (indent < line.Length && line[indent] == ' ')
        {
            indent++;
        }

        if (indent > 3 || indent == line.Length || line[indent] is not ('`' or '~'))
        {
            return null;
        }

        var end = indent;
        while (end < line.Length && line[end] == line[indent])
        {
            end++;
        }

        return end - indent < 3 ? null : (line[indent..end], line[end..], indent);
    }

    private enum FenceKind { Other, CSharp, Unlabelled }

    /// <summary>Whether a type of the given simple name is named anywhere in the document's ✅-marked text (prose or
    /// code; a document without a single status mark counts as ✅ throughout).</summary>
    public static bool NamesType(string document, string simpleName) =>
        Mentions(Classify(document).Where(line => line.Implemented).Select(line => line.Text), simpleName);

    /// <summary>Whether the name is named in ✅ text outside every tree-contract section: the package surface.</summary>
    public static bool NamesTypeInPackageSurface(string document, string simpleName) =>
        Mentions(Classify(document).Where(line => line.Implemented && !line.TreeContract).Select(line => line.Text), simpleName);

    /// <summary>Whether the name is named in ✅ text of a section whose heading carries
    /// <c>(tree contract)</c>.</summary>
    public static bool NamesTypeInTreeContract(string document, string simpleName) =>
        Mentions(Classify(document).Where(line => line.Implemented && line.TreeContract).Select(line => line.Text), simpleName);

    /// <summary>The C# blocks of a document whose nearest status mark above is ✅ (or that have no mark above at all).</summary>
    public static IEnumerable<string> ImplementedCsharpBlocks(string document) => ImplementedSections(document).Select(pair => pair.Block);

    /// <summary>The type names a document's ✅ C# blocks declare, each with whether its section is a tree contract.</summary>
    public static IEnumerable<(string Name, bool TreeContract)> DeclaredTypeSections(string document) =>
        ImplementedSections(document).SelectMany(pair => CSharpSyntaxTree.ParseText(pair.Block).GetRoot().DescendantNodes()
            .OfType<MemberDeclarationSyntax>().Select(DeclaredTypeName).OfType<string>().Select(name => (name, pair.TreeContract)));

    internal static string? DeclaredTypeName(MemberDeclarationSyntax declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax signature => signature.Identifier.ValueText,
        _ => null,
    };

    private static bool Mentions(IEnumerable<string> lines, string simpleName) =>
        Regex.IsMatch(string.Join("\n", lines), $@"\b{Regex.Escape(simpleName)}\b");

    internal static IEnumerable<(string Block, bool TreeContract, int StartLine)> ImplementedSections(string document) =>
        ImplementedFences(document).Where(section => section.Kind == FenceKind.CSharp)
            .Select(section => (section.Block, section.TreeContract, section.StartLine));

    /// <summary>The opening lines of unlabelled implemented blocks that look like API declarations.
    /// Their author must identify the language, since some declaration syntax is shared across languages.</summary>
    internal static IEnumerable<int> UnlabelledDeclarationLines(string document) =>
        ImplementedFences(document).Where(section => section.Kind == FenceKind.Unlabelled && LooksLikeDeclaredApi(section.Block))
            .Select(section => section.StartLine - 1);

    private static bool LooksLikeDeclaredApi(string block)
    {
        static bool ExplicitApi(MemberDeclarationSyntax member) =>
            member.Modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword) || token.IsKind(SyntaxKind.InternalKeyword) ||
                token.IsKind(SyntaxKind.ProtectedKeyword) || token.IsKind(SyntaxKind.PrivateKeyword));

        var unit = SyntaxFactory.ParseCompilationUnit(block);
        if (!unit.ContainsDiagnostics && unit.DescendantNodes().OfType<MemberDeclarationSyntax>()
            .Any(member => DeclaredTypeName(member) is not null && (ExplicitApi(member) || member is RecordDeclarationSyntax)))
        {
            return true;
        }

        // A signature copied without its containing type is still an API-looking block.
        return SyntaxFactory.ParseMemberDeclaration(block) is { ContainsDiagnostics: false } standalone &&
            standalone is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or BaseFieldDeclarationSyntax &&
            ExplicitApi(standalone);
    }

    private static IEnumerable<(string Block, bool TreeContract, int StartLine, FenceKind Kind)> ImplementedFences(string document)
    {
        var block = new List<string>();
        var blockTreeContract = false;
        var blockKind = FenceKind.Other;
        var lineNumber = 0;
        var startLine = 0;
        foreach (var (text, implemented, kind, treeContract) in Classify(document))
        {
            lineNumber++;
            if (implemented && kind != FenceKind.Other)
            {
                if (block.Count == 0)
                {
                    startLine = lineNumber;
                }

                block.Add(text);
                blockTreeContract = treeContract;
                blockKind = kind;
                continue;
            }

            if (block.Count > 0)
            {
                yield return (string.Join("\n", block), blockTreeContract, startLine, blockKind);
                block.Clear();
            }
        }

        if (block.Count > 0)
        {
            yield return (string.Join("\n", block), blockTreeContract, startLine, blockKind);
        }
    }

    /// <summary>
    /// Every line of the document, classified by two state machines. The status one (AGENTS.md §7): a ⏳ or ✅ outside a
    /// fence sets the state until the next mark, so the nearest mark above a block decides it; a mark-like character
    /// inside example code does not count. The tree-contract one: every <c>## </c> heading resets it, and it is on only
    /// under a heading carrying <c>(tree contract)</c>. The fence lines themselves are outside.
    /// </summary>
    private static IEnumerable<(string Text, bool Implemented, FenceKind Kind, bool TreeContract)> Classify(string document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var implemented = true;
        string? opening = null;
        var indent = 0;
        var kind = FenceKind.Other;
        var treeContract = false;
        foreach (var line in document.ReplaceLineEndings("\n").Split('\n'))
        {
            var fence = FenceOf(line);
            if (fence is not null)
            {
                var marker = fence.Value.Marker;
                var info = fence.Value.Info;
                if (opening is null && (marker[0] != '`' || !info.Contains('`', StringComparison.Ordinal)))
                {
                    opening = marker;
                    indent = fence.Value.Indent;
                    var label = info.Trim();
                    var language = label.Split([' ', '\t', ','], 2)[0];
                    kind = label.Length == 0 ? FenceKind.Unlabelled :
                        string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(language, "cs", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(language, "c#", StringComparison.OrdinalIgnoreCase) ? FenceKind.CSharp : FenceKind.Other;
                    yield return (line, implemented, FenceKind.Other, treeContract);
                    continue;
                }

                if (opening is not null && marker[0] == opening[0] && marker.Length >= opening.Length &&
                    info.AsSpan().Trim(" \t").IsEmpty)
                {
                    opening = null;
                    kind = FenceKind.Other;
                    yield return (line, implemented, FenceKind.Other, treeContract);
                    continue;
                }
            }

            if (opening is null)
            {
                if (line.Contains('⏳', StringComparison.Ordinal))
                {
                    implemented = false;
                }
                else if (line.Contains('✅', StringComparison.Ordinal))
                {
                    implemented = true;
                }

                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    treeContract = line.Contains("(tree contract)", StringComparison.Ordinal);
                }
            }

            yield return (kind == FenceKind.Other ? line : Unindent(line, indent), implemented, kind, treeContract);
        }
    }
    private static string Unindent(string line, int indent)
    {
        var first = 0;
        while (first < indent && first < line.Length && line[first] == ' ')
        {
            first++;
        }

        return line[first..];
    }

}
