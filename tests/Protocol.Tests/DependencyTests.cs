namespace APThermo.Protocol.Tests;

/// <summary>
/// Dependencies level: the `## Dependencies` of every node with an assembly equals the nodes whose types its code uses, in the
/// shapes of its types and in the bodies of its methods (a static call names its type in no signature). A parent using the types
/// of its children declares nothing; an ancestor whose own types a node uses is declared like a neighbour (AGENTS.md §6).
/// </summary>
public sealed class DependencyTests
{
    /// <summary>Every node declares, in its own `## Dependencies`, the neighbours it actually uses in signatures and method
    /// bodies, and no other.</summary>
    [Fact]
    public void EveryNodeDeclaresTheNeighboursItUsesAndNoOther()
    {
        var problems = NodeAssemblies.CodeNodes.SelectMany(ProblemsOf).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> ProblemsOf(Node node)
    {
        var (crossings, usedNodes) = Crossings(node);
        var (declared, unresolved) = NodeDocuments.DeclaredDependencies(node);
        var boot = Tree.Relative(node.Boot);
        foreach (var link in unresolved)
        {
            yield return $"{boot} links {link} under ## Dependencies, and no node has that API.md";
        }

        var declaredPaths = declared.Select(d => d.RelativePath).ToHashSet(StringComparer.Ordinal);
        foreach (var used in crossings.Keys.Where(used => !declaredPaths.Contains(used)))
        {
            yield return $"{boot} does not declare {usedNodes[used].Name}, but {node.Name} uses its types: {string.Join(", ", crossings[used].Take(6))}" +
                         (crossings[used].Count > 6 ? $" and {crossings[used].Count - 6} more" : string.Empty);
        }

        foreach (var unused in declared.Where(d => !crossings.ContainsKey(d.RelativePath)).OrderBy(d => d.RelativePath, StringComparer.Ordinal))
        {
            yield return unused.IsDescendantOf(node)
                ? $"{boot} declares its descendant {unused.Name}; a parent owns its children and declares no dependency on them (AGENTS.md §6)"
                : $"{boot} declares {unused.Name}, but no type of {node.Name} refers to it: the dependency went away and the document did not, or it was never real";
        }
    }

    /// <summary>Every neighbour or ancestor node a node's own types (<see cref="NodeAssemblies.TypesOf"/>, not the whole
    /// assembly it compiles into, which a project-less child node may share with others) refer to, and every
    /// (type → referenced type) pair that shows it; <see cref="ProblemsOf"/> names the first six in its message.</summary>
    private static (SortedDictionary<string, SortedSet<string>> Crossings, Dictionary<string, Node> UsedNodes) Crossings(Node node)
    {
        var crossings = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var usedNodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var type in NodeAssemblies.TypesOf(node))
        {
            foreach (var referenced in TypeShape.ReferencedTypes(type))
            {
                var target = NodeAssemblies.NodeOf(referenced);
                if (target is null || target == node || target.IsDescendantOf(node))
                {
                    continue;
                }

                usedNodes[target.RelativePath] = target;
                if (!crossings.TryGetValue(target.RelativePath, out var users))
                {
                    crossings[target.RelativePath] = users = new SortedSet<string>(StringComparer.Ordinal);
                }

                _ = users.Add(TypeShape.SimpleName(TypeShape.Outermost(type)) + " → " + TypeShape.SimpleName(referenced));
            }
        }

        return (crossings, usedNodes);
    }
}
