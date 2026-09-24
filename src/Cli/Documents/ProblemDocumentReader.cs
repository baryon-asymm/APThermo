using APThermo.Execution;

namespace APThermo.Cli.Documents;

/// <summary>
/// Reads a rocket or equilibrium problem document (API.md, Input document): the root of the document. Reads
/// `propellant` through <see cref="PropellantDocumentReader"/>, `problem` through <see cref="ProblemPartReader"/> and
/// `sweep` through <see cref="SweepDocumentReader"/>; reads `engine` itself, finishes the root and assembles the
/// <see cref="InputDocument"/>.
/// </summary>
internal static class ProblemDocumentReader
{
    public static InputDocument Read(string text, string source)
    {
        using var document = JsonText.Parse(text, source);
        try
        {
            var root = new StrictObject(document.RootElement, "$");
            var propellant = PropellantDocumentReader.Read(root.Object("propellant"));
            var problem = ProblemPartReader.Read(root.Object("problem"));
            var sweep = root.OptionalObject("sweep") is { } s ? SweepDocumentReader.Read(s, problem, propellant) : null;
            AcceleratorKind? accelerator = null;
            if (root.OptionalObject("engine") is { } engine)
            {
                accelerator = DocumentWords.ParseAccelerator(engine.String("accelerator"), engine.Path + ".accelerator");
                engine.Finish();
            }

            root.Finish();
            return new InputDocument(propellant, problem, sweep, accelerator);
        }
        catch (InputException e)
        {
            throw new InputException($"{source}: {e.Message}");
        }
    }
}
