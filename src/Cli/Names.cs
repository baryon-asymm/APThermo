using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// Names in documents: camel case of the library's names, and of statuses. The flow, accelerator and problem-kind
/// words are <see cref="DocumentWords"/>' own, next to their parsers, so that a document and an option never learn
/// to spell the same word two ways.
/// </summary>
internal static class Names
{
    public static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    public static string Status(CaseStatus status) => Camel(status.ToString());
}
