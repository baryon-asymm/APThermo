using System.Reflection;

namespace APThermo.Cli;

/// <summary>The catalogue of the JSON schemas embedded from <c>Schemas/</c>: their names and their text, read from the assembly manifest rather than typed, so a file added or removed there is reflected without a second edit.</summary>
internal static class SchemaResources
{
    private const string Prefix = "APThermo.Cli.Schemas.";
    private const string Suffix = ".schema.json";

    private static readonly Assembly Assembly = typeof(SchemaResources).Assembly;

    public static IReadOnlyList<string> Names { get; } = Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Suffix, StringComparison.Ordinal))
        .Select(n => n[Prefix.Length..^Suffix.Length])
        .Order(StringComparer.Ordinal)
        .ToList();

    public static bool TryGet(string name, out string text)
    {
        using var stream = Assembly.GetManifestResourceStream($"{Prefix}{name}{Suffix}");
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
