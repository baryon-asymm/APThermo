using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Tree-contract level (root <c>BOOT.md</c>, Delivery: Tree contracts, 2026-09-15): a node's own <c>API.md</c> keeps two
/// parts, marked in its section headings (<see cref="ApiDeclarations"/>) — the package surface (a heading without the mark)
/// and the tree contract (a heading carrying the text <c>(tree contract)</c>). A public type belongs to the package
/// surface (fact a); a declared type's own section matches its reflected visibility (fact c); a type crossing an assembly
/// boundary through a friend grant is found in the friend's own tree contract (fact b); every such grant itself names a
/// recognised friend (fact e). The split, and facts a and c with it, hold only for a node that packs its own assembly
/// (<see cref="NodeAssemblies.Assemblies"/>), the test assemblies excluded the way <c>SurfaceTests</c> excludes them: a
/// project-less child's types are already visible throughout its ancestor's one assembly with no grant possible, so
/// nothing in its own document needs a mark — exactly <c>src/Execution/Chunks</c> and <c>src/Cli</c>'s five children
/// today, none of them touched by facts a or c.
/// </summary>
public sealed class TreeContractTests
{
    [Fact]
    public void Every_public_type_of_a_library_node_is_named_in_its_package_surface()
    {
        var problems = LibraryNodes().SelectMany(PackageSurfaceProblems).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Every_declared_type_of_a_library_node_matches_its_section_to_its_visibility()
    {
        var problems = LibraryNodes().SelectMany(SectionVisibilityProblems).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void A_type_naming_a_friend_assemblys_internal_type_finds_it_in_the_tree_contract()
    {
        var problems = NodeAssemblies.CodeNodes.SelectMany(TreeContractCrossingProblems).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Every_internals_visible_to_grant_of_a_src_node_names_a_recognised_friend()
    {
        var problems = NodeAssemblies.Assemblies.Keys.Where(node => node.IsSrc).SelectMany(GrantProblems).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The nodes the package-surface/tree-contract split applies to: those that pack their own assembly, the
    /// test assemblies excluded the same way <c>SurfaceTests</c> excludes them.</summary>
    private static IEnumerable<Node> LibraryNodes() =>
        NodeAssemblies.Assemblies.Keys.Where(node => !NodeAssemblies.IsTestAssembly(NodeAssemblies.Assemblies[node]))
            .OrderBy(node => node.RelativePath, StringComparer.Ordinal);

    private static IEnumerable<string> PackageSurfaceProblems(Node node)
    {
        var api = File.ReadAllText(node.Api);
        var exported = NodeAssemblies.Assemblies[node].GetExportedTypes().ToHashSet();
        foreach (var type in NodeAssemblies.TypesOf(node).Where(exported.Contains).OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var name = TypeShape.SimpleName(type);
            if (!ApiDeclarations.NamesTypeInPackageSurface(api, name))
            {
                yield return $"{Tree.Relative(node.Api)} never names {name} in a package-surface section, and {node.Namespace} exports it " +
                             "(root BOOT.md, Delivery: Public surface)";
            }
        }
    }

    /// <summary>Every declared type name of a node's own document, cross-checked against its reflected visibility: a public
    /// type must appear in at least one package-surface section, and an internal type must never appear in one.</summary>
    private static IEnumerable<string> SectionVisibilityProblems(Node node)
    {
        var api = Tree.Relative(node.Api);
        var sections = new Dictionary<string, HashSet<bool>>(StringComparer.Ordinal);
        foreach (var (name, treeContract) in ApiDeclarations.DeclaredTypeSections(File.ReadAllText(node.Api)))
        {
            (sections.TryGetValue(name, out var flags) ? flags : sections[name] = []).Add(treeContract);
        }

        foreach (var (name, flags) in sections.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var problem = SectionVisibilityProblem(node, name, flags);
            if (problem is not null)
            {
                yield return $"{api}: {problem}";
            }
        }
    }

    private static string? SectionVisibilityProblem(Node node, string name, HashSet<bool> sections)
    {
        var type = NodeAssemblies.TypesOf(node).FirstOrDefault(candidate => TypeShape.SimpleName(candidate) == name);
        if (type is null)
        {
            return null; // DeclarationTests already reports a declaration with no matching type.
        }

        if (type.IsVisible && !sections.Contains(false))
        {
            return $"{name} is public, and declared only in a tree-contract section (root BOOT.md, Delivery: Tree contracts)";
        }

        if (!type.IsVisible && sections.Contains(false))
        {
            return $"{name} is internal, and declared in a package-surface section (root BOOT.md, Delivery: Tree contracts)";
        }

        return null;
    }

    /// <summary>Every internal type a node's own code names across an assembly boundary, other than the two exemptions
    /// (root BOOT.md, Delivery: Tree contracts): the assembly it shares with a self or a descendant needs no grant at all,
    /// and a test node's use of the source node it mirrors, its child nodes included, is a design invariant, not a friend
    /// grant to audit here.</summary>
    private static IEnumerable<string> TreeContractCrossingProblems(Node node)
    {
        var ownAssembly = NodeAssemblies.AssemblyOf(node);
        var mirrored = MirroredSourceNode(node);
        foreach (var type in NodeAssemblies.TypesOf(node))
        {
            foreach (var referenced in TypeShape.ReferencedTypes(type))
            {
                var problem = CrossingProblem(node, ownAssembly, mirrored, type, referenced);
                if (problem is not null)
                {
                    yield return problem;
                }
            }
        }
    }

    private static string? CrossingProblem(Node node, Assembly? ownAssembly, Node? mirrored, Type type, Type referenced)
    {
        var target = NodeAssemblies.NodeOf(referenced);
        if (target is null || target == node || target.IsDescendantOf(node) || referenced.IsVisible)
        {
            return null;
        }

        if (NodeAssemblies.AssemblyOf(target) == ownAssembly || (mirrored is not null && (target == mirrored || target.IsDescendantOf(mirrored))))
        {
            return null;
        }

        if (ApiDeclarations.NamesTypeInTreeContract(File.ReadAllText(target.Api), TypeShape.SimpleName(referenced)))
        {
            return null;
        }

        return $"{Tree.Relative(node.Boot)}: {TypeShape.SimpleName(TypeShape.Outermost(type))} names {TypeShape.SimpleName(referenced)}, an " +
               $"internal type of {target.Name}, and {Tree.Relative(target.Api)} does not declare it in a tree-contract section " +
               "(root BOOT.md, Delivery: Tree contracts)";
    }

    /// <summary>The source node a test node mirrors (<c>tests/Y.Tests</c> → <c>src/Y</c>, root BOOT.md, Delivery: Tree
    /// contracts), or null when the node is not shaped that way or names no such node.</summary>
    private static Node? MirroredSourceNode(Node node)
    {
        const string Prefix = "tests/";
        const string Suffix = ".Tests";
        if (!node.RelativePath.StartsWith(Prefix, StringComparison.Ordinal) || !node.RelativePath.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = node.RelativePath[Prefix.Length..^Suffix.Length];
        return Tree.Nodes.FirstOrDefault(candidate => candidate.RelativePath == "src/" + name);
    }

    private static IEnumerable<string> GrantProblems(Node node) => NodeAssemblies.InternalsVisibleTo(NodeAssemblies.Assemblies[node])
        .Select(grant => GrantProblem(node, grant)).Where(problem => problem is not null)!;

    /// <summary>One granted name against the four recognised friends (root BOOT.md, Delivery: Tree contracts):
    /// <c>ILGPURuntime</c>; the node's own mirroring test assembly; the command line, always refused; and otherwise a node
    /// whose own document either declares this node a dependency or, for test infrastructure, actually names one of this
    /// node's own tree-contract types.</summary>
    private static string? GrantProblem(Node node, string grant)
    {
        if (grant == "ILGPURuntime" || grant == node.AssemblyName + ".Tests")
        {
            return null;
        }

        if (grant == "APThermo.Cli")
        {
            return $"{Tree.Relative(node.Boot)}: grants InternalsVisibleTo to APThermo.Cli, but the command line receives no grant " +
                   "(root BOOT.md, Delivery: Tree contracts)";
        }

        var granteeAssembly = NodeAssemblies.Assemblies.Values.FirstOrDefault(assembly => assembly.GetName().Name == grant);
        var grantee = granteeAssembly is null ? null : NodeAssemblies.NodeOf(granteeAssembly);
        if (grantee is null || granteeAssembly is null)
        {
            return $"{Tree.Relative(node.Boot)}: grants InternalsVisibleTo to {grant}, which is not a node of the tree and not ILGPURuntime";
        }

        return GrantIsJustified(node, grantee, granteeAssembly)
            ? null
            : $"{Tree.Relative(node.Boot)}: grants InternalsVisibleTo to {grant}, but {Tree.Relative(grantee.Boot)} neither declares " +
              $"{node.Name} in its ## Dependencies nor names a tree-contract type of it (root BOOT.md, Delivery: Tree contracts)";
    }

    private static bool GrantIsJustified(Node node, Node grantee, Assembly granteeAssembly)
    {
        var (declared, _) = NodeDocuments.DeclaredDependencies(grantee);
        if (declared.Contains(node))
        {
            return true;
        }

        var isTestInfrastructure = grantee.RelativePath is "tests/Benchmarks" or "tests/Harness" || NodeAssemblies.IsTestAssembly(granteeAssembly);
        return isTestInfrastructure && NamesTreeContractType(granteeAssembly, node);
    }

    private static bool NamesTreeContractType(Assembly grantee, Node granter)
    {
        var api = File.ReadAllText(granter.Api);
        return grantee.GetTypes().SelectMany(TypeShape.ReferencedTypes)
            .Where(referenced => NodeAssemblies.NodeOf(referenced) == granter && !referenced.IsVisible)
            .Any(referenced => ApiDeclarations.NamesTypeInTreeContract(api, TypeShape.SimpleName(referenced)));
    }
}
