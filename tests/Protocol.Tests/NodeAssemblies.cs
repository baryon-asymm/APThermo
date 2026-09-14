using System.Reflection;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>The assembly each node's project builds, loaded from this project's build output; a type or an assembly to its node.</summary>
internal static class NodeAssemblies
{
    private static readonly Lazy<IReadOnlyDictionary<Node, Assembly>> AssembliesLazy = new(Load);

    /// <summary>The assemblies of the nodes that have a project, loaded by the name the project gives them.</summary>
    public static IReadOnlyDictionary<Node, Assembly> Assemblies => AssembliesLazy.Value;

    /// <summary>The node whose project built the assembly, or null for an assembly from outside the tree.</summary>
    public static Node? NodeOf(Assembly assembly) => Assemblies.FirstOrDefault(pair => pair.Value == assembly).Key;

    public static Node? NodeOf(Type type) => NodeOf(type.Assembly);

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
