using System.Reflection;

namespace APThermo.Cli;

/// <summary>The embedded JSON schemas of the command line's documents, read from the assembly manifest.</summary>
internal static class SchemaResources
{
    private static readonly Assembly Assembly = typeof(SchemaResources).Assembly;

    public static IReadOnlyList<string> Names { get; } = new[] { "input", "output", "states", "species", "devices" };

    public static bool TryGet(string name, out string text)
    {
        var resourceName = $"APThermo.Cli.Schemas.{name}.schema.json";
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            text = string.Empty;
            return false;
        }

        using var reader = new StreamReader(stream);
        text = reader.ReadToEnd();
        return true;
    }
}
