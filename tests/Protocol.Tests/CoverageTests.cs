using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Coverage level: every type a library assembly exports is named in the API.md of its node (asking <see cref="ApiDeclarations"/>,
/// the one meaning of "named"), and every type of every assembly lives in the namespace of its node (AGENTS.md §1: the namespace
/// repeats the directory path, which is how the checks assign a type to a node). The snapshot cannot catch an added and
/// undescribed type: it is generated from the same code.
/// </summary>
public sealed class CoverageTests
{
    /// <summary>Every exported type of a library assembly is named in its node's API.md.</summary>
    [Fact]
    public void EveryExportedTypeOfALibraryAssemblyIsNamedInItsNodesApi()
    {
        var problems = NodeAssemblies.CodeNodes
            .Where(node => !NodeAssemblies.IsTestAssembly(NodeAssemblies.AssemblyOf(node)!))
            .SelectMany(UndocumentedTypeProblems)
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Every type of every assembly resolves, by its own namespace, to exactly one node's own namespace (not merely
    /// a deeper, undeclared corner of an ancestor's), and that node's own code compiles into the assembly the type was found
    /// in — the generalisation AGENTS.md §1 needs once several nodes can share one assembly (root BOOT.md, Constraints,
    /// 2026-09-15): a type whose namespace only a prefix of some node matches, with no node of the exact deeper path, is
    /// still misplaced.</summary>
    [Fact]
    public void EveryTypeOfEveryAssemblyLivesInTheNamespaceOfItsNode()
    {
        var problems = NodeAssemblies.Assemblies.Values.Distinct().OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .SelectMany(MisplacedTypeProblems)
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> UndocumentedTypeProblems(Node node)
    {
        var api = File.ReadAllText(node.Api);
        var exported = NodeAssemblies.AssemblyOf(node)!.GetExportedTypes().ToHashSet();
        foreach (var type in NodeAssemblies.TypesOf(node).Where(exported.Contains).OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var name = TypeShape.SimpleName(type);
            if (!ApiDeclarations.NamesType(api, name))
            {
                yield return $"{Tree.Relative(node.Api)} never names {name}, which {node.Namespace} exports (root BOOT.md, Taboos: no public type outside its node's API.md)";
            }
        }
    }

    private static IEnumerable<string> MisplacedTypeProblems(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsNested || type.Namespace is null || TypeShape.IsCompilerGenerated(type))
            {
                continue;
            }

            var owner = NodeAssemblies.NodeOf(type);
            if (owner is null || owner.Namespace != type.Namespace)
            {
                yield return $"{type.FullName} is in namespace {type.Namespace}, and no node of the tree is exactly that namespace (AGENTS.md §1)";
            }
            else if (NodeAssemblies.AssemblyOf(owner) != assembly)
            {
                yield return $"{type.FullName} is in namespace {type.Namespace}; its node {owner.Name} compiles into " +
                             $"{NodeAssemblies.AssemblyOf(owner)!.GetName().Name}, not {assembly.GetName().Name} (AGENTS.md §1)";
            }
        }
    }
}
