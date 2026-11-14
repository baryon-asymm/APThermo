namespace APThermo.Protocol.Tests;

/// <summary>Every declaration in an implemented C# block must match a specific member of the assembled tree.</summary>
public sealed class DeclarationTests
{
    /// <summary>Implemented declaration-like examples identify their language before signature verification.</summary>
    [Fact]
    public void DeclarationBlocksIdentifyTheirLanguage()
    {
        var problems = Tree.Nodes.SelectMany(node => ApiDeclarations.UnlabelledDeclarationLines(File.ReadAllText(node.Api))
            .Select(line => $"{Tree.Relative(node.Api)}:{line}: unlabelled API declaration block; use csharp for C# or label the example's language")).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Checks the documented owner, kind and overload directly against compiled metadata.</summary>
    [Fact]
    public void EveryDeclarationUnderATickExists()
    {
        var sections = Tree.Nodes.SelectMany(node => ApiDeclarations.ImplementedSections(File.ReadAllText(node.Api))
            .Select(section => (Node: node, Section: section))).ToList();
        Assert.NotEmpty(sections);
        var problems = sections.SelectMany(entry => Declaration.Problems(entry.Section.Block,
            entry.Node.Namespace,
            Tree.Relative(entry.Node.Api), entry.Section.StartLine)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
