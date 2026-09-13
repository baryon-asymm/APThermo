using System.Reflection;
using System.Text.RegularExpressions;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// Declarations level: every type and member declared in a C# block of an API.md under a ✅ heading exists in an assembly of the
/// tree, the node's own assembly first. Names, not signatures: the signatures are pinned by the snapshot, and a second copy of
/// them here would be a second thing to keep in step. A block under ⏳ is a sketch and is not read; a document without a mark
/// counts as ✅ throughout (AGENTS.md §7).
/// </summary>
public sealed class DeclarationTests
{
    [Fact]
    public void Every_declaration_under_a_tick_exists()
    {
        var problems = new List<string>();
        var checkedBlocks = 0;
        foreach (var node in Tree.Nodes)
        {
            var api = Tree.Relative(node.Api);
            foreach (var block in ImplementedCsharpBlocks(File.ReadAllText(node.Api)))
            {
                checkedBlocks++;
                Type? current = null;
                var typesInBlock = new HashSet<string>(StringComparer.Ordinal);
                foreach (var declaration in Declarations(block))
                {
                    if (declaration.IsType)
                    {
                        typesInBlock.Add(declaration.Name);
                        current = Find(node, declaration.Name);
                        if (current is null)
                        {
                            problems.Add($"{api}: declares the type {declaration.Name} under ✅, and no assembly of the tree has it");
                        }

                        continue;
                    }

                    // A member named after a type of the block is a constructor, which reflection reports as .ctor.
                    if (current is null || typesInBlock.Contains(declaration.Name) || (declaration.IsEnumMember && !current.IsEnum))
                    {
                        continue;
                    }

                    if (!HasMember(current, declaration.Name))
                    {
                        problems.Add($"{api}: {Tree.SimpleName(current)} has no member named {declaration.Name}, declared under ✅");
                    }
                }
            }
        }

        Assert.True(checkedBlocks > 0, "no C# block under ✅ was found in any API.md; the parser lost the documents");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The type of the given simple name: in the node's own assembly first, then in any assembly of the tree.</summary>
    private static Type? Find(Node node, string simpleName)
    {
        var own = Tree.Assemblies.TryGetValue(node, out var assembly) ? assembly : null;
        return Tree.Assemblies.Values
            .OrderBy(candidate => candidate == own ? 0 : 1)
            .ThenBy(candidate => candidate.GetName().Name, StringComparer.Ordinal)
            .SelectMany(candidate => candidate.GetTypes())
            .FirstOrDefault(type => Tree.SimpleName(type) == simpleName);
    }

    private static bool HasMember(Type type, string name)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return type.GetMember(name, Any).Length > 0 || type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic) is not null;
    }

    /// <summary>The C# blocks of a document that sit under the nearest status mark above them being ✅ (or no mark at all).</summary>
    public static IEnumerable<string> ImplementedCsharpBlocks(string document)
    {
        var implemented = true;
        var inside = false;
        var block = new List<string>();
        foreach (var line in document.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inside)
                {
                    if (implemented)
                    {
                        yield return string.Join("\n", block);
                    }

                    block.Clear();
                    inside = false;
                }
                else if (line.Contains("csharp", StringComparison.Ordinal))
                {
                    inside = true;
                }

                continue;
            }

            if (inside)
            {
                block.Add(line);
            }
            else if (line.Contains('⏳', StringComparison.Ordinal))
            {
                implemented = false;
            }
            else if (line.Contains('✅', StringComparison.Ordinal))
            {
                implemented = true;
            }
        }
    }

    /// <summary>
    /// The names a C# block declares: types (class, struct, record, enum, interface, delegate), the positional parameters of
    /// records, properties, methods, fields (several per line), enum members. Lines inside an open parameter list are skipped.
    /// </summary>
    public static IEnumerable<Declaration> Declarations(string block)
    {
        var lines = block.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = StripComment(lines[index]);
            if (line.Length == 0 || line.TrimStart().StartsWith("using ", StringComparison.Ordinal)
                || line.TrimStart().StartsWith("namespace ", StringComparison.Ordinal) || line.TrimStart().StartsWith('['))
            {
                continue;
            }

            var type = Regex.Match(line, @"\b(?:record\s+struct|record\s+class|record|class|struct|enum|interface|delegate\s+[\w<>\[\],.?]+)\s+(\w+)");
            if (type.Success)
            {
                yield return new Declaration(type.Groups[1].Value, IsType: true, IsEnumMember: false);
                foreach (var parameter in ParameterList(lines, ref index, line))
                {
                    yield return new Declaration(parameter, IsType: false, IsEnumMember: false);
                }

                continue;
            }

            var property = Regex.Match(line, @"\b(\w+)\s*\{\s*(?:get|set|init)");
            if (property.Success)
            {
                yield return new Declaration(property.Groups[1].Value, IsType: false, IsEnumMember: false);
                continue;
            }

            var method = Regex.Match(line, @"\b(\w+)\s*(?:<[\w,\s]+>)?\s*\(");
            if (method.Success && !IsKeyword(method.Groups[1].Value))
            {
                yield return new Declaration(method.Groups[1].Value, IsType: false, IsEnumMember: false);
                SkipOpenList(lines, ref index, line);
                continue;
            }

            var field = Regex.Match(
                line,
                @"^\s*(?:(?:public|internal|private|protected|static|readonly|const|required|new|volatile|unsafe)\s+)*[\w.]+(?:<[^;=]*>)?(?:\[[\s,]*\])*\??\s+(?<names>\w+(?:\s*=\s*[^,;]+)?(?:\s*,\s*\w+(?:\s*=\s*[^,;]+)?)*)\s*;\s*$");
            if (field.Success)
            {
                foreach (var declarator in field.Groups["names"].Value.Split(','))
                {
                    var name = Regex.Match(declarator, @"^\s*(\w+)");
                    if (name.Success && !IsKeyword(name.Groups[1].Value))
                    {
                        yield return new Declaration(name.Groups[1].Value, IsType: false, IsEnumMember: false);
                    }
                }

                continue;
            }

            var enumMember = Regex.Match(line, @"^\s*(\w+)\s*(?:=\s*[^,]+?)?\s*,?\s*$");
            if (enumMember.Success && !IsKeyword(enumMember.Groups[1].Value))
            {
                yield return new Declaration(enumMember.Groups[1].Value, IsType: false, IsEnumMember: true);
            }
        }
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
