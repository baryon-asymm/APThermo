using System.Reflection;

namespace APThermo.Protocol.Tests;

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
        var problems = Tree.Nodes.SelectMany(ProblemsOf).ToList();
        if (!Tree.Nodes.Any(HasImplementedBlocks))
        {
            problems.Add("no C# block under ✅ was found in any API.md; the parser lost the documents");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static bool HasImplementedBlocks(Node node) => ApiDeclarations.ImplementedCsharpBlocks(File.ReadAllText(node.Api)).Any();

    private static IEnumerable<string> ProblemsOf(Node node)
    {
        var api = Tree.Relative(node.Api);
        foreach (var block in ApiDeclarations.ImplementedCsharpBlocks(File.ReadAllText(node.Api)))
        {
            foreach (var problem in ProblemsInBlock(node, api, block))
            {
                yield return problem;
            }
        }
    }

    /// <summary>The problems of one ✅ block: a declared type no assembly has, or a declared member its type does not have. A
    /// member named after a type of the block is a constructor, which reflection reports as .ctor, and is skipped.</summary>
    private static IEnumerable<string> ProblemsInBlock(Node node, string api, string block)
    {
        Type? current = null;
        var typesInBlock = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in ApiDeclarations.Declarations(block))
        {
            if (declaration.IsType)
            {
                typesInBlock.Add(declaration.Name);
                current = Find(node, declaration.Name);
                if (current is null)
                {
                    yield return $"{api}: declares the type {declaration.Name} under ✅, and no assembly of the tree has it";
                }

                continue;
            }

            if (current is null || typesInBlock.Contains(declaration.Name) || (declaration.IsEnumMember && !current.IsEnum))
            {
                continue;
            }

            if (!HasMember(current, declaration.Name))
            {
                yield return $"{api}: {TypeShape.SimpleName(current)} has no member named {declaration.Name}, declared under ✅";
            }
        }
    }

    /// <summary>The type of the given simple name: attributed to the node itself first, then anywhere in its own effective
    /// assembly (<see cref="NodeAssemblies.AssemblyOf"/>, a project-less child node's own types included), then in any
    /// assembly of the tree — the generalisation of "the node's own assembly first" once a node's code need not be its own
    /// assembly (root BOOT.md, Constraints, 2026-09-15).</summary>
    private static Type? Find(Node node, string simpleName)
    {
        var ownAssembly = NodeAssemblies.AssemblyOf(node);
        return NodeAssemblies.Assemblies.Values.Distinct()
            .SelectMany(candidate => candidate.GetTypes())
            .Where(type => TypeShape.SimpleName(type) == simpleName)
            .OrderBy(type => NodeAssemblies.NodeOf(type) == node ? 0 : type.Assembly == ownAssembly ? 1 : 2)
            .ThenBy(type => type.Assembly.GetName().Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool HasMember(Type type, string name)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return type.GetMember(name, Any).Length > 0 || type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic) is not null;
    }
}
