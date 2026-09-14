namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// A node of the tree: a directory holding both documents. <see cref="RelativePath"/> is the directory path from the tree root
/// with '/' separators, empty for the root; <see cref="AssemblyName"/> is the name of the project in the directory, if any.
/// </summary>
internal sealed record Node(string RelativePath, string Directory, string? AssemblyName)
{
    public string Name => RelativePath.Length == 0 ? "the root" : RelativePath;

    public string Boot => Path.Combine(Directory, "BOOT.md");

    public string Api => Path.Combine(Directory, "API.md");

    public bool IsDescendantOf(Node other) =>
        other.RelativePath.Length == 0 ? RelativePath.Length > 0 : RelativePath.StartsWith(other.RelativePath + "/", StringComparison.Ordinal);
}
