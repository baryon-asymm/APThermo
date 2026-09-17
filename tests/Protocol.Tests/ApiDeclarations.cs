using System.Text.RegularExpressions;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The grammar of an API.md: its ✅ C# blocks and the declarations in them, and the text that sits under ✅ more broadly. The one
/// meaning of "named in the API.md" for <c>DeclarationTests</c> (a specific declaration exists, read from the ✅ C# blocks) and
/// <c>CoverageTests</c> (every exported type is at least named, by prose or by code, under a ✅ mark): both ask this type rather
/// than carrying two separate ideas of what counts as documented. A type is not always given a full declaration: a struct public
/// only because a framework requires it (for instance ILGPU's kernel parameters) may be named in a sentence instead, and that
/// still counts.
/// </summary>
internal static class ApiDeclarations
{
    /// <summary>Whether a type of the given simple name is named anywhere in the document's ✅-marked text (prose or code alike;
    /// a document without a single status mark counts as ✅ throughout, AGENTS.md §7).</summary>
    public static bool NamesType(string document, string simpleName) =>
        Regex.IsMatch(string.Join("\n", Classify(document).Where(line => line.Implemented).Select(line => line.Text)), $@"\b{Regex.Escape(simpleName)}\b");

    /// <summary>Whether a type of the given simple name is named in the document's ✅-marked, package-surface text (root
    /// <c>BOOT.md</c>, Delivery: Tree contracts) — a section whose heading carries no <c>(tree contract)</c> mark.</summary>
    public static bool NamesTypeInPackageSurface(string document, string simpleName) => NamesTypeInSection(document, simpleName, treeContract: false);

    /// <summary>Whether a type of the given simple name is named in the document's ✅-marked, tree-contract text (root
    /// <c>BOOT.md</c>, Delivery: Tree contracts) — a section whose heading carries the <c>(tree contract)</c> mark.</summary>
    public static bool NamesTypeInTreeContract(string document, string simpleName) => NamesTypeInSection(document, simpleName, treeContract: true);

    private static bool NamesTypeInSection(string document, string simpleName, bool treeContract) =>
        Regex.IsMatch(
            string.Join("\n", Classify(document).Where(line => line.Implemented && line.TreeContract == treeContract).Select(line => line.Text)),
            $@"\b{Regex.Escape(simpleName)}\b");

    /// <summary>The C# blocks of a document that sit under the nearest status mark above them being ✅ (or no mark at all).</summary>
    public static IEnumerable<string> ImplementedCsharpBlocks(string document) => ImplementedCsharpBlocksBySection(document).Select(pair => pair.Block);

    /// <summary>The names a document's ✅-marked C# blocks declare as types, paired with whether the block's own section is a
    /// tree contract (root <c>BOOT.md</c>, Delivery: Tree contracts).</summary>
    public static IEnumerable<(string Name, bool TreeContract)> DeclaredTypeSections(string document) =>
        ImplementedCsharpBlocksBySection(document).SelectMany(pair => Declarations(pair.Block).Where(d => d.IsType).Select(d => (d.Name, pair.TreeContract)));

    /// <summary>The C# blocks of a document that sit under the nearest status mark above them being ✅ (or no mark at all), each
    /// paired with whether its own section is a tree contract: the state <see cref="Classify"/> reads from the heading line that
    /// opened the block's section, frozen for the whole block the same way the ✅/⏳ state is.</summary>
    private static IEnumerable<(string Block, bool TreeContract)> ImplementedCsharpBlocksBySection(string document)
    {
        var block = new List<string>();
        var blockImplemented = false;
        var blockTreeContract = false;
        var wasInsideBlock = false;
        foreach (var (text, implemented, insideBlock, treeContract) in Classify(document))
        {
            if (insideBlock)
            {
                block.Add(text);
                blockImplemented = implemented;
                blockTreeContract = treeContract;
                wasInsideBlock = true;
                continue;
            }

            if (wasInsideBlock)
            {
                if (blockImplemented)
                {
                    yield return (string.Join("\n", block), blockTreeContract);
                }

                block.Clear();
                wasInsideBlock = false;
            }
        }
    }

    /// <summary>
    /// Every line of the document, classified by two independent state machines: the ✅/⏳ status-mark one AGENTS.md §7
    /// describes (whether the line sits under an effective ✅, or no mark at all, rather than ⏳), and the tree-contract one
    /// (root <c>BOOT.md</c>, Delivery: Tree contracts): whether the line sits under a section heading (a line starting
    /// <c>"## "</c>) that carries the text <c>(tree contract)</c>, package surface otherwise. Both machines read a mark only on
    /// a heading line or, for ✅/⏳, anywhere outside a ```csharp fence; the two differ in how a mark's absence is read.
    /// ✅/⏳ persists until the next mark, so a heading with neither leaves it as the nearest mark above left it. The
    /// tree-contract mark does not persist across headings: every <c>"## "</c> line resets it, marked or not, because a
    /// section's own heading is where AGENTS.md §7's own state-of-the-art model has nothing to say and the split needs its own,
    /// simpler rule — a section is a tree contract only when its own heading says so. Also tracks whether the line itself lies
    /// inside a ```csharp fence (the fence marker lines do not); a mark is read only outside such a fence — the nearest mark
    /// above a code block decides the block's fate, not a character that happens to look like a mark inside the example code
    /// the block holds — so a line inside a fence carries the state as of the line that opened it, frozen for the whole block.
    /// <see cref="NamesType"/>, <see cref="NamesTypeInSection"/> and <see cref="ImplementedCsharpBlocksBySection"/> all read
    /// this one walk, so both mark rules are written once.
    /// </summary>
    private static IEnumerable<(string Text, bool Implemented, bool InsideCSharpBlock, bool TreeContract)> Classify(string document)
    {
        var implemented = true;
        var inside = false;
        var treeContract = false;
        foreach (var line in document.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inside)
                {
                    inside = false;
                }
                else if (line.Contains("csharp", StringComparison.Ordinal))
                {
                    inside = true;
                }

                yield return (line, implemented, false, treeContract);
                continue;
            }

            if (!inside)
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

            yield return (line, implemented, inside, treeContract);
        }
    }

    /// <summary>
    /// The names a C# block declares: types (class, struct, record, enum, interface, delegate), the positional parameters of
    /// records, properties, methods, fields (several per line), enum members. Lines inside an open parameter list are skipped.
    /// Tries each declaration kind in turn on every line; the first that matches wins.
    /// </summary>
    public static IEnumerable<Declaration> Declarations(string block)
    {
        var lines = block.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = StripComment(lines[index]);
            if (IsSkippable(line))
            {
                continue;
            }

            foreach (var declaration in OnLine(lines, ref index, line))
            {
                yield return declaration;
            }
        }
    }

    private static bool IsSkippable(string line) =>
        line.Length == 0 || line.TrimStart().StartsWith("using ", StringComparison.Ordinal)
            || line.TrimStart().StartsWith("namespace ", StringComparison.Ordinal) || line.TrimStart().StartsWith('[');

    /// <summary>The declarations of one line: a type first (which may open a parameter list spanning further lines), then a
    /// property, a method (which may open a parameter list too), a field list, or an enum member.</summary>
    private static IReadOnlyList<Declaration> OnLine(string[] lines, ref int index, string line)
    {
        var type = TypeDeclaration(lines, ref index, line);
        if (type is not null)
        {
            return type;
        }

        var property = PropertyDeclaration(line);
        if (property is not null)
        {
            return [property.Value];
        }

        var method = MethodDeclaration(lines, ref index, line);
        if (method is not null)
        {
            return [method.Value];
        }

        var fields = FieldDeclarations(line);
        if (fields is not null)
        {
            return fields;
        }

        var enumMember = EnumMemberDeclaration(line);
        return enumMember is null ? [] : [enumMember.Value];
    }

    /// <summary>A type declaration (class, struct, record, enum, interface, delegate) and the positional parameters of a
    /// record, following the parameter list across lines when it opens one.</summary>
    private static IReadOnlyList<Declaration>? TypeDeclaration(string[] lines, ref int index, string line)
    {
        var match = Regex.Match(line, @"\b(?:record\s+struct|record\s+class|record|class|struct|enum|interface|delegate\s+[\w<>\[\],.?]+)\s+(\w+)");
        if (!match.Success)
        {
            return null;
        }

        var declarations = new List<Declaration> { new(match.Groups[1].Value, IsType: true, IsEnumMember: false) };
        foreach (var parameter in ParameterList(lines, ref index, line))
        {
            declarations.Add(new Declaration(parameter, IsType: false, IsEnumMember: false));
        }

        return declarations;
    }

    private static Declaration? PropertyDeclaration(string line)
    {
        var match = Regex.Match(line, @"\b(\w+)\s*\{\s*(?:get|set|init)");
        return match.Success ? new Declaration(match.Groups[1].Value, IsType: false, IsEnumMember: false) : null;
    }

    /// <summary>A method, constructor or operator declaration; skips over its parameter list when it spans further lines.</summary>
    private static Declaration? MethodDeclaration(string[] lines, ref int index, string line)
    {
        var match = Regex.Match(line, @"\b(\w+)\s*(?:<[\w,\s]+>)?\s*\(");
        if (!match.Success || IsKeyword(match.Groups[1].Value))
        {
            return null;
        }

        SkipOpenList(lines, ref index, line);
        return new Declaration(match.Groups[1].Value, IsType: false, IsEnumMember: false);
    }

    private static IReadOnlyList<Declaration>? FieldDeclarations(string line)
    {
        var match = Regex.Match(
            line,
            @"^\s*(?:(?:public|internal|private|protected|static|readonly|const|required|new|volatile|unsafe)\s+)*[\w.]+(?:<[^;=]*>)?(?:\[[\s,]*\])*\??\s+(?<names>\w+(?:\s*=\s*[^,;]+)?(?:\s*,\s*\w+(?:\s*=\s*[^,;]+)?)*)\s*;\s*$");
        if (!match.Success)
        {
            return null;
        }

        var declarations = new List<Declaration>();
        foreach (var declarator in match.Groups["names"].Value.Split(','))
        {
            var name = Regex.Match(declarator, @"^\s*(\w+)");
            if (name.Success && !IsKeyword(name.Groups[1].Value))
            {
                declarations.Add(new Declaration(name.Groups[1].Value, IsType: false, IsEnumMember: false));
            }
        }

        return declarations;
    }

    private static Declaration? EnumMemberDeclaration(string line)
    {
        var match = Regex.Match(line, @"^\s*(\w+)\s*(?:=\s*[^,]+?)?\s*,?\s*$");
        return match.Success && !IsKeyword(match.Groups[1].Value) ? new Declaration(match.Groups[1].Value, IsType: false, IsEnumMember: true) : null;
    }

    /// <summary>The parameter names of a positional record, following the list across lines; empty for a type without one.</summary>
    private static List<string> ParameterList(string[] lines, ref int index, string first)
    {
        var names = new List<string>();
        if (!first.Contains('(', StringComparison.Ordinal))
        {
            return names;
        }

        var text = Collect(lines, ref index, first);
        var open = text.IndexOf('(', StringComparison.Ordinal);
        var close = text.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return names;
        }

        foreach (var parameter in text[(open + 1)..close].Split(','))
        {
            var withoutDefault = parameter.Split('=')[0].Trim();
            var name = Regex.Match(withoutDefault, @"(\w+)\s*$");
            if (name.Success && char.IsUpper(name.Groups[1].Value[0]))
            {
                names.Add(name.Groups[1].Value);
            }
        }

        return names;
    }

    private static void SkipOpenList(string[] lines, ref int index, string first) => Collect(lines, ref index, first);

    /// <summary>The line and its continuation lines until the parentheses balance.</summary>
    private static string Collect(string[] lines, ref int index, string first)
    {
        var text = first;
        var depth = Depth(first);
        while (depth > 0 && index + 1 < lines.Length)
        {
            index++;
            var next = StripComment(lines[index]);
            text += " " + next;
            depth += Depth(next);
        }

        return text;

        static int Depth(string line) => line.Count(c => c == '(') - line.Count(c => c == ')');
    }

    private static string StripComment(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return (comment >= 0 ? line[..comment] : line).TrimEnd();
    }

    private static bool IsKeyword(string word) =>
        word is "if" or "for" or "foreach" or "while" or "switch" or "return" or "new" or "get" or "set" or "init" or "throw"
            or "using" or "nameof" or "typeof" or "default" or "sizeof" or "var" or "operator" or "where" or "else" or "do" or "in";

    public readonly record struct Declaration(string Name, bool IsType, bool IsEnumMember);
}
