namespace APThermo.Protocol.Tests;

/// <summary>
/// The one rule <see cref="CompiledSources"/> applies beyond the tree root and <c>bin</c>/<c>obj</c>: a compiled source
/// belongs to a restored NuGet package, and so is not the tree's, exactly when an ancestor directory holds a
/// <c>*.nupkg.metadata</c> file (2026-09-30, the CI cache inside the workspace). Runs on a scratch directory of its own, so
/// that it needs no package cache in the tree.
/// </summary>
public sealed class CompiledSourcesTests
{
    /// <summary>A source below a package folder is the package's, the same source without the sentinel is the tree's, and so
    /// is every source in a directory <c>templates</c>, <c>artifacts</c> or a dot-directory (the guards audit's F5); a
    /// sentinel at the root never claims a source, so a tree that itself lies in a package folder is still read.</summary>
    [Fact]
    public void OnlyASourceBelowAPackageFolderIsThePackages()
    {
        var scratch = Directory.CreateTempSubdirectory("apthermo-compiled-sources-").FullName;
        try
        {
            var root = scratch + Path.DirectorySeparatorChar;
            var package = Path.Combine(scratch, ".nuget-packages", "some.package", "1.0.0");
            var packageSource = WriteSource(Path.Combine(package, "build", "net8.0", "Program.cs"));
            File.WriteAllText(Path.Combine(package, ".nupkg.metadata"), "{}");
            var treeSource = WriteSource(Path.Combine(scratch, "src", "Node", "Code.cs"));
            var templatesSource = WriteSource(Path.Combine(scratch, "src", "Node", "templates", "Code.cs"));
            var artifactsSource = WriteSource(Path.Combine(scratch, "artifacts", "Code.cs"));
            var dotSource = WriteSource(Path.Combine(scratch, ".hidden", "Code.cs"));

            Assert.True(CompiledSources.IsPackageOwned(packageSource, root), "a source below a package folder was read as the tree's");
            Assert.False(CompiledSources.IsPackageOwned(treeSource, root), "a plain node source was read as a package's");
            Assert.False(CompiledSources.IsPackageOwned(templatesSource, root), "a source in templates was read as a package's");
            Assert.False(CompiledSources.IsPackageOwned(artifactsSource, root), "a source in artifacts was read as a package's");
            Assert.False(CompiledSources.IsPackageOwned(dotSource, root), "a source in a dot-directory was read as a package's");

            File.WriteAllText(Path.Combine(scratch, ".nupkg.metadata"), "{}");
            Assert.False(CompiledSources.IsPackageOwned(treeSource, root), "a sentinel at the root claimed a source of the tree");
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static string WriteSource(string file)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "// scratch\n");
        return file;
    }
}
