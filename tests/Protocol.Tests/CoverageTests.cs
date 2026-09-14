namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// Coverage level: every type a library assembly exports is named in the API.md of its node (asking <see cref="ApiDeclarations"/>,
/// the one meaning of "named"), and every type of every assembly lives in the namespace of its node (AGENTS.md §1: the namespace
/// repeats the directory path, which is how the checks assign a type to a node). The snapshot cannot catch an added and
/// undescribed type: it is generated from the same code.
/// </summary>
public sealed class CoverageTests
{
    [Fact]
    public void Every_exported_type_of_a_library_assembly_is_named_in_its_nodes_api()
    {
        var problems = new List<string>();
        foreach (var (node, assembly) in NodeAssemblies.Assemblies.OrderBy(pair => pair.Key.RelativePath, StringComparer.Ordinal))
        {
            if (NodeAssemblies.IsTestAssembly(assembly))
            {
                continue;
            }

            var api = File.ReadAllText(node.Api);
            foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                var name = TypeShape.SimpleName(type);
                if (!ApiDeclarations.NamesType(api, name))
                {
                    problems.Add($"{Tree.Relative(node.Api)} never names {name}, which {node.AssemblyName} exports (root BOOT.md, Taboos: no public type outside its node's API.md)");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Every_type_of_every_assembly_lives_in_the_namespace_of_its_node()
    {
        var problems = new List<string>();
        foreach (var (node, assembly) in NodeAssemblies.Assemblies.OrderBy(pair => pair.Key.RelativePath, StringComparer.Ordinal))
        {
            foreach (var type in assembly.GetTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                if (type.IsNested || type.Namespace is null || TypeShape.IsCompilerGenerated(type))
                {
                    continue;
                }

                if (type.Namespace != node.AssemblyName)
                {
                    problems.Add($"{type.FullName} is in namespace {type.Namespace}; the node {node.Name} is the namespace {node.AssemblyName} (AGENTS.md §1)");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
