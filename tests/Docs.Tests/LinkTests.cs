using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L4 (BOOT.md): every relative link of README.md, llms.txt and the markdown under docs/ resolves to an existing
/// file, in every form markdown or HTML carries one — an inline link, a reference-style link (`[text][id]` /
/// `[text][]` against a `[id]: target` definition), and an HTML `href`; external http(s) links are out, except a
/// link into this repository's own public copy — `https://github.com/baryon-asymm/APThermo/blob/main/…` or
/// `.../tree/main/…` — which must resolve to an existing file or directory of the tree the same way a relative one
/// does. A `#anchor` fragment, bare or on a file link, must name an existing heading of its target document (the
/// same document when the link carries no file), by GitHub's own slug (lowercase, everything but a letter, digit,
/// space or hyphen dropped, a space turned into a hyphen) — so a renamed heading breaks the link that pointed at it
/// instead of silently rendering nowhere. Only docs/protocol/templates is excluded — its placeholder links are
/// deliberate (AGENTS.md §13); every other document under docs/protocol is checked like any other page. The package
/// READMEs under docs/nuget/ may carry no relative link at all: nuget.org renders them outside the repository, where
/// a relative link breaks; they may carry a self-repository link instead, and it is checked the same way. No
/// document, package READMEs included, may carry the placeholder `OWNER/REPO` (its finding M5, fixed in `657410d`):
/// a link built on it resolves nowhere once packed. Fails when the document set is empty, and asserts that
/// README.md and llms.txt themselves exist (m1: a check that silently has nothing to check when they are deleted is
/// indistinguishable from an absent one).
/// </summary>
public sealed class LinkTests
{
    private const string SelfRepositoryBlobPrefix = "https://github.com/baryon-asymm/APThermo/blob/main/";
    private const string SelfRepositoryTreePrefix = "https://github.com/baryon-asymm/APThermo/tree/main/";

    private static readonly Regex InlineLink = new(@"\[[^\]]*\]\(\s*([^)\s]+)", RegexOptions.Compiled);
    private static readonly Regex ReferenceUsage = new(@"\[([^\]]+)\]\[([^\]]*)\]", RegexOptions.Compiled);
    private static readonly Regex ReferenceDefinition = new(@"^\s{0,3}\[([^\]]+)\]:\s*(\S+)", RegexOptions.Compiled);
    private static readonly Regex HtmlHref = new(@"href\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled);
    private static readonly Regex Scheme = new(@"^[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.Compiled);
    private static readonly Regex Heading = new(@"^ {0,3}#{1,6}\s+(\S.*)$", RegexOptions.Compiled);

    [Fact]
    public void The_root_guide_entry_points_exist()
    {
        Assert.True(File.Exists(Path.Combine(GuideDocuments.Root, "README.md")), "README.md does not exist at the repository root");
        Assert.True(File.Exists(Path.Combine(GuideDocuments.Root, "llms.txt")), "llms.txt does not exist at the repository root");
    }

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
                Assert.True(Scheme.IsMatch(target.Path), $"{path}: '{target.Path}' is a relative link; nuget.org renders this page outside the repository, so it must be absolute");
            }
        }
    }

    [Fact]
    public void Every_self_repository_link_of_a_package_readme_resolves_to_a_file_or_directory()
    {
        var readmes = GuideDocuments.NugetReadmes();
        Assert.True(readmes.Count > 0, "no package README was found under docs/nuget/");
        foreach (var path in readmes)
        {
            CheckDocument(path);
        }
    }

    [Fact]
    public void No_document_carries_the_OWNER_REPO_placeholder()
    {
        var documents = GuideDocuments.LinkedDocuments().Concat(GuideDocuments.NugetReadmes()).ToList();
        Assert.True(documents.Count > 0, "no document (README.md, llms.txt, docs/**/*.md) was found");
        foreach (var path in documents)
        {
            var lines = GuideDocuments.Lines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                Assert.True(!lines[i].Contains("OWNER/REPO", StringComparison.Ordinal), $"{path}:{i + 1}: carries the placeholder 'OWNER/REPO'; a real repository link is required before release");
            }
        }
    }

    private static void CheckDocument(string path)
    {
        var lines = GuideDocuments.Lines(path);
        var headingsByFile = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var target in TargetsOf(path))
        {
            string resolvedFile;
            if (target.Path.Length == 0)
            {
                resolvedFile = path; // a bare "#fragment": the same document
            }
            else if (TryStripSelfRepositoryPrefix(target.Path, out var repoRelative))
            {
                resolvedFile = Path.GetFullPath(Path.Combine(GuideDocuments.Root, Uri.UnescapeDataString(repoRelative)));
                Assert.True(
                    File.Exists(resolvedFile) || Directory.Exists(resolvedFile),
                    $"{path}: the link '{target.Path}' does not resolve to an existing file or directory of the repository");
            }
            else if (Scheme.IsMatch(target.Path))
            {
                continue; // external: http(s), mailto, … (not a link into this repository's own tree)
            }
            else
            {
                var directory = Path.GetDirectoryName(path)!;
                resolvedFile = Path.GetFullPath(Path.Combine(directory, Uri.UnescapeDataString(target.Path)));
                Assert.True(File.Exists(resolvedFile), $"{path}: the link '{target.Path}' does not resolve to an existing file");
            }

            if (target.Fragment is null)
            {
                continue;
            }

            if (!headingsByFile.TryGetValue(resolvedFile, out var slugs))
            {
                slugs = SlugsOf(resolvedFile);
                headingsByFile[resolvedFile] = slugs;
            }

            Assert.True(
                slugs.Contains(target.Fragment, StringComparer.Ordinal),
                $"{path}: the anchor '#{target.Fragment}' (of '{(target.Path.Length == 0 ? "this document" : target.Path)}') names no heading; known anchors: {string.Join(", ", slugs)}");
        }
    }

    /// <summary>Every link target of a document — inline, reference-style and HTML — with its file part and fragment (anchor) split apart. In-page anchors and site-rooted links are excluded from the file part alone; their fragment, when present, is still returned against the empty file part.</summary>
    private static IEnumerable<(string Path, string? Fragment)> TargetsOf(string path)
    {
        var lines = GuideDocuments.Lines(path);
        var mask = GuideDocuments.OutsideFenceMask(lines);
        var references = ReferenceDefinitionsOf(lines, mask);

        for (var i = 0; i < lines.Length; i++)
        {
            if (!mask[i])
            {
                continue;
            }

            foreach (var target in TargetsOfLine(lines[i], references))
            {
                yield return target;
            }
        }
    }

    /// <summary>Every `[id]: target` reference definition of a document, outside its fences.</summary>
    private static Dictionary<string, string> ReferenceDefinitionsOf(string[] lines, bool[] mask)
    {
        var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!mask[i])
            {
                continue;
            }

            var definition = ReferenceDefinition.Match(lines[i]);
            if (definition.Success)
            {
                references[definition.Groups[1].Value] = definition.Groups[2].Value;
            }
        }

        return references;
    }

    /// <summary>Every inline, reference-style and HTML link target of one line, backtick spans stripped for the markdown forms (an HTML `href` may sit inside backticks in prose, so it is scanned on the raw line).</summary>
    private static IEnumerable<(string Path, string? Fragment)> TargetsOfLine(string line, IReadOnlyDictionary<string, string> references)
    {
        var stripped = Regex.Replace(line, "`[^`]*`", "");
        foreach (Match match in InlineLink.Matches(stripped))
        {
            foreach (var split in SplitTarget(match.Groups[1].Value))
            {
                yield return split;
            }
        }

        foreach (var split in ReferenceTargetsOf(stripped, references))
        {
            yield return split;
        }

        foreach (Match match in HtmlHref.Matches(line))
        {
            foreach (var split in SplitTarget(match.Groups[1].Value))
            {
                yield return split;
            }
        }
    }

    /// <summary>The targets of a line's reference-style usages (`[text][id]` / `[text][]`) resolved against the document's definitions; a usage with no matching definition names nothing.</summary>
    private static IEnumerable<(string Path, string? Fragment)> ReferenceTargetsOf(string strippedLine, IReadOnlyDictionary<string, string> references)
    {
        foreach (Match match in ReferenceUsage.Matches(strippedLine))
        {
            var id = match.Groups[2].Value.Length > 0 ? match.Groups[2].Value : match.Groups[1].Value;
            if (!references.TryGetValue(id, out var target))
            {
                continue;
            }

            foreach (var split in SplitTarget(target))
            {
                yield return split;
            }
        }
    }

    /// <summary>A raw link target split into its file part (site-rooted "/..." excluded entirely) and its "#fragment", when one is present.</summary>
    private static IEnumerable<(string Path, string? Fragment)> SplitTarget(string rawTarget)
    {
        if (rawTarget.StartsWith('/'))
        {
            yield break; // site-rooted rather than relative to the document
        }

        var withoutQuery = rawTarget.Split('?')[0];
        var hashIndex = withoutQuery.IndexOf('#');
        var file = hashIndex < 0 ? withoutQuery : withoutQuery[..hashIndex];
        var fragment = hashIndex < 0 ? null : withoutQuery[(hashIndex + 1)..];

        if (file.Length == 0 && fragment is null)
        {
            yield break;
        }

        yield return (file, fragment);
    }

    /// <summary>
    /// Strips this repository's own "blob/main/" or "tree/main/" GitHub prefix from an absolute link, so the rest
    /// resolves against the repository root the way a relative link resolves against its document's directory. An
    /// absolute link with any other origin (nuget.org, another repository, a different branch or commit) is left
    /// alone; the caller then treats it as ordinary external and skips it.
    /// </summary>
    private static bool TryStripSelfRepositoryPrefix(string target, out string repositoryRelativePath)
    {
        if (target.StartsWith(SelfRepositoryBlobPrefix, StringComparison.Ordinal))
        {
            repositoryRelativePath = target[SelfRepositoryBlobPrefix.Length..];
            return true;
        }

        if (target.StartsWith(SelfRepositoryTreePrefix, StringComparison.Ordinal))
        {
            repositoryRelativePath = target[SelfRepositoryTreePrefix.Length..];
            return true;
        }

        repositoryRelativePath = "";
        return false;
    }

    /// <summary>Every heading of a markdown document, as GitHub's own anchor slugs: lowercase, everything but a letter, digit, space or hyphen dropped, a space turned into a hyphen. A duplicate heading's later slugs carry a "-1", "-2", … suffix, as GitHub disambiguates them.</summary>
    private static IReadOnlyList<string> SlugsOf(string path)
    {
        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return [];
        }

        var slugs = new List<string>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in GuideDocuments.OutsideFences(GuideDocuments.Lines(path)))
        {
            var match = Heading.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var slug = SlugOf(match.Groups[1].Value);
            if (seen.TryGetValue(slug, out var count))
            {
                seen[slug] = count + 1;
                slug = $"{slug}-{count}";
            }
            else
            {
                seen[slug] = 1;
            }

            slugs.Add(slug);
        }

        return slugs;
    }

    private static string SlugOf(string heading)
    {
        var kept = heading.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-');
        return new string(kept.ToArray()).Replace(' ', '-');
    }
}
