using System.Reflection;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// The coupling measurements of the Shape level ("Shape check": efferent coupling, stable type, stable dependencies): per type
/// from the same IL walk <see cref="DependencyTests"/> uses (<see cref="TypeShape.ReferencedTypes"/>), not from syntax; per
/// <c>src</c> node from the dependencies the nodes' <c>BOOT.md</c> declare (<see cref="NodeDocuments"/>). Asserts nothing: a
/// figure here is compared with a limit, or with a node's declared exception, by <c>ShapeTests</c>.
/// </summary>
internal static class CouplingMeasures
{
    private static readonly Lazy<IReadOnlyList<(Type Source, Type Target)>> EdgesLazy = new(BuildEdges);

    /// <summary>
    /// Every type's efferent coupling: the distinct types of the tree it names in its signatures and method bodies, a nested
    /// or compiler-generated type's own naming attributed to the outermost type that declares it (so only outermost types are
    /// keyed here), a constructed generic type counted once as its definition, types outside the tree not counted. A type that
    /// names nothing of the tree carries no entry (its figure is zero).
    /// </summary>
    public static IReadOnlyDictionary<Type, int> EfferentCoupling() => EdgesLazy.Value
        .GroupBy(edge => edge.Source)
        .ToDictionary(group => group.Key, group => group.Select(edge => edge.Target).Distinct().Count());

    /// <summary>
    /// Every type's afferent coupling: the distinct outermost types of the <c>src</c> nodes that name it, by the same edges
    /// as <see cref="EfferentCoupling"/> read the other way around, kept only where the naming type's own node is a
    /// <c>src</c> node (root BOOT.md, the stable-type sentence: "named by 10 or more types of the `src` nodes"; a test
    /// node's use of a type never makes it stable). Unlike the source side, a target keeps its own identity even when it is
    /// a nested type: the "stable type" rule is asked of every type of the <c>src</c> nodes, nested ones included.
    /// </summary>
    public static IReadOnlyDictionary<Type, int> AfferentCoupling() => EdgesLazy.Value
        .Where(edge => NodeAssemblies.NodeOf(edge.Source) is { IsSrc: true })
        .GroupBy(edge => edge.Target)
        .ToDictionary(group => group.Key, group => group.Select(edge => edge.Source).Distinct().Count());

    /// <summary>
    /// The "stable dependencies" measure over the `src` project graph: for every `src` node, the other `src` nodes it depends
    /// on (`Ce`, its out-degree), the other `src` nodes that depend on it (`Ca`, its in-degree), and the set of nodes it
    /// depends on, so that a caller can both compute `I = Ce / (Ca + Ce)` and walk every edge. Read from the declared
    /// dependencies (<see cref="NodeDocuments"/>), which the Dependencies level holds equal to the nodes whose types each
    /// node's code uses; the project references themselves are not read. Test nodes and the ancestors/descendants a node's
    /// own dependency section may never name play no part.
    /// </summary>
    public static IReadOnlyDictionary<Node, (int Ce, int Ca, IReadOnlySet<Node> Dependencies)> NodeCoupling()
    {
        var srcNodes = Tree.Nodes.Where(node => node.IsSrc).ToHashSet();
        var dependencies = srcNodes.ToDictionary(node => node, DependenciesOf);
        return srcNodes.ToDictionary(node => node, node => Coupling(node, srcNodes, dependencies));

        IReadOnlySet<Node> DependenciesOf(Node node) => NodeDocuments.DeclaredDependencies(node).Nodes.Where(srcNodes.Contains).ToHashSet();
    }

    private static (int Ce, int Ca, IReadOnlySet<Node> Dependencies) Coupling(
        Node node, IReadOnlySet<Node> srcNodes, IReadOnlyDictionary<Node, IReadOnlySet<Node>> dependencies)
    {
        var dependents = srcNodes.Count(other => dependencies[other].Contains(node));
        return (dependencies[node].Count, dependents, dependencies[node]);
    }

    private static IReadOnlyList<(Type Source, Type Target)> BuildEdges()
    {
        var edges = new List<(Type Source, Type Target)>();
        foreach (var assembly in NodeAssemblies.Assemblies.Values.Distinct())
        {
            CollectEdges(assembly, edges);
        }

        return edges;
    }

    private static void CollectEdges(Assembly assembly, List<(Type Source, Type Target)> edges)
    {
        foreach (var family in assembly.GetTypes().GroupBy(TypeShape.Outermost).Where(family => !TypeShape.IsCompilerGenerated(family.Key)))
        {
            var members = family.ToHashSet();
            foreach (var referenced in family.SelectMany(TypeShape.ReferencedTypes))
            {
                var target = referenced.IsConstructedGenericType ? referenced.GetGenericTypeDefinition() : referenced;
                if (members.Contains(target) || TypeShape.IsCompilerGenerated(target) || NodeAssemblies.NodeOf(target) is null)
                {
                    continue;
                }

                edges.Add((family.Key, target));
            }
        }
    }
}
