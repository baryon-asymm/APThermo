using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L4 (BOOT.md): every relative link of README.md, llms.txt and the markdown under docs/ resolves to an existing
/// file; external http(s) links and in-page anchors are out. Only docs/protocol/templates is excluded — its
/// placeholder links are deliberate (AGENTS.md §13); every other document under docs/protocol is checked like any
/// other page. The package READMEs under docs/nuget/ may carry no relative link at all: nuget.org renders them
/// outside the repository, where a relative link breaks. Fails when the document set is empty.
/// </summary>
public sealed class LinkTests
{
    private static readonly Regex Link = new(@"\[[^\]]*\]\(\s*([^)\s]+)", RegexOptions.Compiled);
    private static readonly Regex Scheme = new(@"^[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.Compiled);

    [Fact]
    public void Every_relative_link_resolves_to_a_file()
    {
        var documents = GuideDocuments.LinkedDocuments();
        Assert.True(documents.Count > 0, "no linked document (README.md, llms.txt, docs/**/*.md) was found");
        foreach (var path in documents)
        {
            CheckDocument(path);
        }
    }

    [Fact]
    public void No_package_readme_carries_a_relative_link()
    {
        var readmes = GuideDocuments.NugetReadmes();
        Assert.True(readmes.Count > 0, "no package README was found under docs/nuget/");
        foreach (var path in readmes)
        {
            foreach (var target in TargetsOf(path))
            {
                Assert.True(Scheme.IsMatch(target), $"{path}: '{target}' is a relative link; nuget.org renders this page outside the repository, so it must be absolute");
            }
        }
    }

    private static void CheckDocument(string path)
    {
        foreach (var target in TargetsOf(path))
        {
            if (Scheme.IsMatch(target))
            {
                continue; // external: http(s), mailto, …
            }

            var directory = Path.GetDirectoryName(path)!;
            var resolved = Path.GetFullPath(Path.Combine(directory, Uri.UnescapeDataString(target)));
            Assert.True(File.Exists(resolved), $"{path}: the link '{target}' does not resolve to an existing file");
        }
    }

    /// <summary>The link targets of a document, in-page anchors and absolute (site-rooted) links already excluded.</summary>
    private static IEnumerable<string> TargetsOf(string path)
    {
        foreach (var line in GuideDocuments.OutsideFences(GuideDocuments.Lines(path)))
        {
            var stripped = Regex.Replace(line, @"`[^`]*`", "");
            foreach (Match match in Link.Matches(stripped))
            {
                var target = match.Groups[1].Value;
                if (target.StartsWith('#') || target.StartsWith('/'))
                {
                    continue; // in-page anchor, or site-rooted rather than relative to the document
                }

                target = target.Split('#')[0].Split('?')[0];
                if (target.Length > 0)
                {
                    yield return target;
                }
            }
        }
    }
}
