using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>The assembly each node's project builds, loaded from this project's build output; a type's own node by the
/// namespace attribution AGENTS.md §1 defines (the deepest node whose namespace equals, or prefixes at a dot boundary, the
/// type's own namespace) — the one attribution every reflection check of this node reads, project node and project-less
/// child node alike (root <c>BOOT.md</c>, Constraints, 2026-09-15).</summary>
internal static class NodeAssemblies
{
    private static readonly Lazy<IReadOnlyDictionary<Node, Assembly>> AssembliesLazy = new(Load);

    private static readonly Lazy<IReadOnlyList<Node>> CodeNodesLazy = new(
        () => Tree.Nodes.Where(node => AssemblyOf(node) is not null).OrderBy(node => node.RelativePath, StringComparer.Ordinal).ToList());

    /// <summary>The assemblies of the nodes that have a project, loaded by the name the project gives them.</summary>
    public static IReadOnlyDictionary<Node, Assembly> Assemblies => AssembliesLazy.Value;

    /// <summary>Every node whose code lives in one of the tree's assemblies: a node with its own project, or a project-less
    /// child node compiling into its nearest ancestor's (<see cref="AssemblyOf"/>). Excludes the tree root, which holds no
    /// project of its own and no ancestor to fall back to. Ordered by path; what the Shape, Coverage, Declarations and
    /// Dependencies levels iterate instead of the project nodes alone, now that a node's code need not be its own assembly.</summary>
    public static IReadOnlyList<Node> CodeNodes => CodeNodesLazy.Value;

    /// <summary>The node whose project holds a node's compiled types: itself if it has a project, or the nearest ancestor
    /// that does (root <c>BOOT.md</c>, Constraints, 2026-09-15). Null only for a node with no project anywhere in its own
    /// chain up to the root — the tree root itself, which holds no code of its own. The one "nearest ancestor with a
    /// project" walk every caller needing that attribution reads, <see cref="AssemblyOf"/> and
    /// <see cref="CouplingMeasures.NodeCoupling"/> (the stable-dependencies measure, root <c>BOOT.md</c>, Constraints,
    /// 2026-09-15 child-nodes phase) included.</summary>
    public static Node? ProjectNodeOf(Node node) =>
        Tree.Nodes.Where(candidate => candidate == node || node.IsDescendantOf(candidate))
            .OrderByDescending(candidate => candidate.RelativePath.Length)
            .FirstOrDefault(candidate => Assemblies.ContainsKey(candidate));

    /// <summary>The assembly that holds a node's compiled types: its own project's assembly if it has one, or the nearest
    /// ancestor's that does (<see cref="ProjectNodeOf"/>). Null only for a node with no project anywhere in its own chain up
    /// to the root — the tree root itself, which holds no code of its own.</summary>
    public static Assembly? AssemblyOf(Node node)
    {
        var projectNode = ProjectNodeOf(node);
        return projectNode is null ? null : Assemblies[projectNode];
    }

    /// <summary>The node whose project built the assembly, or null for an assembly from outside the tree. Distinct from
    /// <see cref="NodeOf(Type)"/>: this names only the project node, never a project-less child whose code the same
    /// assembly also carries.</summary>
    public static Node? NodeOf(Assembly assembly) => Assemblies.FirstOrDefault(pair => pair.Value == assembly).Key;

    /// <summary>The node a type belongs to: the deepest node of the tree whose namespace equals, or prefixes at a dot
    /// boundary, the type's own namespace (AGENTS.md §1). A type with no namespace of its own at all (a top-level,
    /// unnamed compiler helper the author never declared, such as `&lt;PrivateImplementationDetails&gt;` or an anonymous
    /// type, none of which carries the `CompilerGeneratedAttribute` a caller could filter on) falls back to the project
    /// node of the assembly it physically sits in, exactly as every reflection check here read it before a node's code
    /// could live in more than its own assembly — such a type names no node of its own to attribute it to more precisely.
    /// Null only when neither resolves: a type truly from outside the tree. Never silently skipped: reported by whichever
    /// check meets such a type.</summary>
    public static Node? NodeOf(Type type) => NodeOfNamespace(type.Namespace) ?? NodeOf(type.Assembly);

    /// <summary>The deepest node whose <see cref="Node.Namespace"/> equals, or prefixes at a dot boundary, the given
    /// namespace; null when no node matches at all. The one namespace-attribution walk <see cref="NodeOf(Type)"/> uses.</summary>
    public static Node? NodeOfNamespace(string? ns)
    {
        if (ns is null)
        {
            return null;
        }

        Node? best = null;
        foreach (var node in Tree.Nodes)
        {
            if ((ns == node.Namespace || ns.StartsWith(node.Namespace + ".", StringComparison.Ordinal))
                && (best is null || node.Namespace.Length > best.Namespace.Length))
            {
                best = node;
            }
        }

        return best;
    }

    /// <summary>Every type reflection reports for a node's own effective assembly (<see cref="AssemblyOf"/>) whose own
    /// namespace resolves, by <see cref="NodeOf(Type)"/>, to this node and no deeper one: the set the tree's checks read as
    /// "the node's own types" now that several nodes can share one assembly. Empty for a node with no assembly at all.</summary>
    public static IEnumerable<Type> TypesOf(Node node)
    {
        var assembly = AssemblyOf(node);
        return assembly is null ? [] : assembly.GetTypes().Where(type => NodeOf(type) == node);
    }

    /// <summary>A test assembly references xunit; its public types are its tests, listed by its BOOT.md, not by API.md.</summary>
    public static bool IsTestAssembly(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Any(reference => reference.Name is { } name && name.StartsWith("xunit", StringComparison.Ordinal));

    private static IReadOnlyDictionary<Node, Assembly> Load()
    {
        var assemblies = new Dictionary<Node, Assembly>();
        foreach (var node in Tree.Nodes.Where(node => node.AssemblyName is not null))
        {
            try
            {
                assemblies[node] = Assembly.Load(new AssemblyName(node.AssemblyName!));
            }
            catch (FileNotFoundException e)
            {
                throw new InvalidOperationException(
                    $"the assembly of {node.Name} ({node.AssemblyName}) is not in the build output of Protocol.Tests: " +
                    "add a project reference to it in this node's project file", e);
            }
        }

        return assemblies;
    }
}
