using System.Runtime.CompilerServices;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// Where the tree is and what its nodes are: the root found from this source file (AGENTS.md §13: from the source, never from
/// the binary), the nodes found by directory path (a directory holding both documents, the kit's templates and the build
/// directories skipped), and paths relative to the root.
/// </summary>
internal static class Tree
{
    /// <summary>Directories never read as nodes or as source: build output, tool caches, the agent's session directory (.claude
    /// holds worktrees of other sessions, each a full copy of the tree), and the protocol kit's templates (the linter's
    /// --exclude templates). Internal rather than private so that <see cref="SourceSyntax"/> shares the one list instead of
    /// keeping a second copy that could drift from it.</summary>
    internal static readonly HashSet<string> Skipped = new(StringComparer.Ordinal)
    {
        ".git", ".vs", ".claude", "bin", "obj", ".venv", "__pycache__", "TestResults", "artifacts", "node_modules", "templates",
    };

    private static readonly Lazy<string> RootLazy = new(FindRoot);

    private static readonly Lazy<IReadOnlyList<Node>> NodesLazy = new(FindNodes);

    /// <summary>The directory holding AGENTS.md, found upward from this file (AGENTS.md §13: from the source, never from the binary).</summary>
    public static string Root => RootLazy.Value;

    /// <summary>Every node of the tree, ordered by path; the root first.</summary>
    public static IReadOnlyList<Node> Nodes => NodesLazy.Value;

    public static string Relative(string path)
    {
        var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
        return relative == "." ? string.Empty : relative;
    }

    private static string FindRoot()
    {
        var directory = Path.GetDirectoryName(ThisFile());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "AGENTS.md")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("the tree root (a directory with AGENTS.md) was not found above " + ThisFile());
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static IReadOnlyList<Node> FindNodes()
    {
        var nodes = new List<Node>();
        Walk(Root);
        return nodes.OrderBy(node => node.RelativePath, StringComparer.Ordinal).ToList();

        void Walk(string directory)
        {
            if (File.Exists(Path.Combine(directory, "BOOT.md")) && File.Exists(Path.Combine(directory, "API.md")))
            {
                var projects = System.IO.Directory.GetFiles(directory, "*.csproj");
                if (projects.Length > 1)
                {
                    throw new InvalidOperationException($"{Relative(directory)} holds {projects.Length} projects; a node has one assembly (root BOOT.md)");
                }

                nodes.Add(new Node(Relative(directory), directory, projects.Length == 1 ? Path.GetFileNameWithoutExtension(projects[0]) : null));
            }

            foreach (var child in System.IO.Directory.GetDirectories(directory))
            {
                if (!Skipped.Contains(Path.GetFileName(child)))
                {
                    Walk(child);
                }
            }
        }
    }
}
