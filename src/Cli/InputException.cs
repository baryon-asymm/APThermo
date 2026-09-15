namespace APThermo.Cli;

/// <summary>What the user gave cannot be used: an option, a document, a path; exit code 2.</summary>
internal sealed class InputException(string message) : Exception(message);
