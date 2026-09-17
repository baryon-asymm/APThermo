namespace APThermo.Protocol.Tests;

/// <summary>
/// A node of the tree: a directory holding both documents. <see cref="RelativePath"/> is the directory path from the tree root
/// with '/' separators, empty for the root; <see cref="AssemblyName"/> is the name of the project in the directory, if any.
/// </summary>
internal sealed record Node(string RelativePath, string Directory, string? AssemblyName)
{
    /// <summary>The root namespace of the tree (root <c>BOOT.md</c>, Constraints): every node's <see cref="Namespace"/> starts
    /// from here, and it is the one place that name is written.</summary>
    private const string RootNamespace = "APThermo";

    public string Name => RelativePath.Length == 0 ? "the root" : RelativePath;

    public string Boot => Path.Combine(Directory, "BOOT.md");

    public string Api => Path.Combine(Directory, "API.md");

    /// <summary>The C# namespace this node's own code lives in (AGENTS.md §1: the namespace repeats the directory path from
    /// the tree root; root <c>BOOT.md</c>, Constraints: `src`, `tests` and `samples` are transparent). Equal to <see cref="AssemblyName"/>
    /// for every node that holds a project, by the same convention the build follows; the only definition a project-less
    /// child node has (root <c>BOOT.md</c>, Constraints, 2026-09-15: such a node compiles into its nearest ancestor's
    /// assembly, under its own namespace). The one attribution every reflection check in this node reads
    /// (<see cref="NodeAssemblies.NodeOf(Type)"/>).</summary>
    public string Namespace => RelativePath.Length == 0
        ? RootNamespace
        : RootNamespace + "." + string.Join('.', RelativePath.Split('/').Where(segment => segment is not ("src" or "tests" or "samples")));

    public bool IsDescendantOf(Node other) =>
        other.RelativePath.Length == 0 ? RelativePath.Length > 0 : RelativePath.StartsWith(other.RelativePath + "/", StringComparison.Ordinal);

    /// <summary>Whether this is one of the `src` nodes the root's code-shape constraint's coupling and stable-type rules
    /// are scoped to (root BOOT.md, the stable-type and efferent-coupling sentences; `tests/Protocol.Tests/BOOT.md`,
    /// "Shape check"). The one definition <see cref="CouplingMeasures"/> and <c>ShapeTests</c> both read, so the two
    /// cannot drift apart the way their own separate copies of this same test once could (R-Protocol.Tests-14).</summary>
    public bool IsSrc => RelativePath.StartsWith("src/", StringComparison.Ordinal);
}
