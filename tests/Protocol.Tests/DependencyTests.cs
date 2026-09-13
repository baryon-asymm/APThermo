namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// Dependencies level: the `## Dependencies` of every node with an assembly equals the nodes whose types its code uses, in the
/// shapes of its types and in the bodies of its methods (a static call names its type in no signature). A parent using the types
/// of its children declares nothing; an ancestor whose own types a node uses is declared like a neighbour (AGENTS.md §6).
/// </summary>
public sealed class DependencyTests
{
    [Fact]
    public void Every_node_declares_the_neighbours_it_uses_and_no_other()
    {
        var problems = new List<string>();
        foreach (var (node, assembly) in Tree.Assemblies.OrderBy(pair => pair.Key.RelativePath, StringComparer.Ordinal))
        {
            var crossings = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var usedNodes = new Dictionary<string, Node>(StringComparer.Ordinal);
            foreach (var type in assembly.GetTypes())
            {
                foreach (var referenced in Tree.ReferencedTypes(type))
                {
                    var target = Tree.NodeOf(referenced);
                    if (target is null || target == node || target.IsDescendantOf(node))
                    {
                        continue;
                    }

                    usedNodes[target.RelativePath] = target;
                    if (!crossings.TryGetValue(target.RelativePath, out var users))
                    {
                        crossings[target.RelativePath] = users = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    users.Add(Tree.SimpleName(Tree.Outermost(type)) + " → " + Tree.SimpleName(referenced));
                }
            }

            var (declared, unresolved) = Tree.DeclaredDependencies(node);
            var boot = Tree.Relative(node.Boot);
            foreach (var link in unresolved)
            {
                problems.Add($"{boot} links {link} under ## Dependencies, and no node has that API.md");
            }

            var declaredPaths = declared.Select(d => d.RelativePath).ToHashSet(StringComparer.Ordinal);
            foreach (var used in crossings.Keys.Where(used => !declaredPaths.Contains(used)))
            {
                problems.Add($"{boot} does not declare {usedNodes[used].Name}, but {node.Name} uses its types: {string.Join(", ", crossings[used].Take(6))}" +
                             (crossings[used].Count > 6 ? $" and {crossings[used].Count - 6} more" : string.Empty));
            }

            foreach (var unused in declared.Where(d => !crossings.ContainsKey(d.RelativePath)).OrderBy(d => d.RelativePath, StringComparer.Ordinal))
            {
                problems.Add(unused.IsDescendantOf(node)
                    ? $"{boot} declares its descendant {unused.Name}; a parent owns its children and declares no dependency on them (AGENTS.md §6)"
                    : $"{boot} declares {unused.Name}, but no type of {node.Name} refers to it: the dependency went away and the document did not, or it was never real");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
