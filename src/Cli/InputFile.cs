namespace APThermo.Cli;

/// <summary>Reading a user-named input file, with the missing-file message this node documents.</summary>
internal static class InputFile
{
    public static string ReadAllText(string path) =>
        !File.Exists(path) ? throw new InputException($"input file not found: {path}") : File.ReadAllText(path);
}
