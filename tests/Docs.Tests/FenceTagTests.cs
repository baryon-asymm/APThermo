namespace APThermo.Docs.Tests;

/// <summary>
/// L3's fence-tag allow-list (BOOT.md, minor 3 of the fourth documentation review): every fence of README.md,
/// docs/guide/*.md and the package READMEs under docs/nuget/ carries an info string whose first word is one of the
/// three tags this node reads by — `csharp`/`cs`/`c#` for L1, `json` for the cli-document facts and L5, `console`
/// for a shown shell session. Before this task an untagged fence, or one with an unrecognised tag, reached no check
/// at all: a broken JSON or C# example with no language word on its fence would silently escape L1 and the
/// cli-document facts alike, since each of them looks only for its own tag and skips whatever does not carry it.
/// </summary>
public sealed class FenceTagTests
{
    [Fact]
    public void Every_fence_carries_a_tag_from_the_allow_list()
    {
        var fences = new List<(string File, int Line, string Info)>();
        foreach (var file in GuideDocuments.SnippetSources())
        {
            var lines = GuideDocuments.Lines(file);
            foreach (var block in GuideDocuments.FencedBlocks(lines, file))
            {
                fences.Add((file, block.StartLine, block.Info));
            }
        }

        Assert.True(fences.Count > 0, "no fenced code block was found in README.md, docs/guide/*.md or docs/nuget/*.md");

        foreach (var (file, line, info) in fences)
        {
            CheckAllowListed(file, line, info);
        }
    }

    private static void CheckAllowListed(string file, int line, string info)
    {
        var recognized = GuideDocuments.IsCSharpFenceInfo(info)
            || GuideDocuments.IsJsonFenceInfo(info)
            || GuideDocuments.IsConsoleFenceInfo(info);

        var word = GuideDocuments.FirstWord(info);
        Assert.True(
            recognized,
            $"{file}:{line + 1}: this fence carries no tag from the allow-list (csharp, json, console): "
                + (word.Length == 0 ? "the fence is untagged" : $"found '{word}'"));
    }
}
