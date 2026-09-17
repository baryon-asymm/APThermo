using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L4 (BOOT.md): every relative link of README.md, llms.txt and the markdown under docs/ resolves to an existing
/// file; external http(s) links and in-page anchors are out. The protocol kit (docs/protocol) is out as well: its
/// templates carry deliberate &lt;placeholder&gt; links that never resolve, so a check red on them by construction
/// would be worse than absent (AGENTS.md §13). Passes vacuously while none of the documents exists.
/// </summary>
public sealed class LinkTests
{
    private static readonly Regex Link = new(@"\[[^\]]*\]\(\s*([^)\s]+)", RegexOptions.Compiled);
    private static readonly Regex Scheme = new(@"^[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.Compiled);

    [Fact]
    public void Every_relative_link_resolves_to_a_file()
    {
        foreach (var path in GuideDocuments.LinkedDocuments())
        {
            CheckDocument(path);
        }
    }

    private static void CheckDocument(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        foreach (var line in GuideDocuments.OutsideFences(GuideDocuments.Lines(path)))
        {
            var stripped = Regex.Replace(line, @"`[^`]*`", "");
            foreach (Match match in Link.Matches(stripped))
            {
                var target = match.Groups[1].Value;
                if (target.StartsWith('#'))
                {
                    continue; // in-page anchor
                }

                if (Scheme.IsMatch(target))
                {
                    continue; // external: http(s), mailto, …
                }

                if (target.StartsWith('/'))
                {
                    continue; // absolute, not relative to the document
                }

                target = target.Split('#')[0].Split('?')[0];
                if (target.Length == 0)
                {
                    continue; // a bare fragment after all
                }

                var resolved = Path.GetFullPath(Path.Combine(directory, Uri.UnescapeDataString(target)));
                Assert.True(File.Exists(resolved), $"{path}: the link '{match.Value}' does not resolve to an existing file");
            }
        }
    }
}
