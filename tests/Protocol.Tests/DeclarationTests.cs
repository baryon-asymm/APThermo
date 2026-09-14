using System.Reflection;

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
            foreach (var block in ApiDeclarations.ImplementedCsharpBlocks(File.ReadAllText(node.Api)))
            {
                checkedBlocks++;
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
                        problems.Add($"{api}: {TypeShape.SimpleName(current)} has no member named {declaration.Name}, declared under ✅");
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
        var own = NodeAssemblies.Assemblies.TryGetValue(node, out var assembly) ? assembly : null;
        return NodeAssemblies.Assemblies.Values
            .OrderBy(candidate => candidate == own ? 0 : 1)
            .ThenBy(candidate => candidate.GetName().Name, StringComparer.Ordinal)
            .SelectMany(candidate => candidate.GetTypes())
            .FirstOrDefault(type => TypeShape.SimpleName(type) == simpleName);
    }

    private static bool HasMember(Type type, string name)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return type.GetMember(name, Any).Length > 0 || type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic) is not null;
    }
}
