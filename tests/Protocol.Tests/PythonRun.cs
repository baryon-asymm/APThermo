namespace APThermo.Protocol.Tests;

/// <summary>The outcome of one Python process: its exit code and standard output followed by standard error.</summary>
/// <param name="ExitCode">The exit code of the script.</param>
/// <param name="Output">The script's decoded standard output followed by standard error.</param>
internal sealed record PythonRun(int ExitCode, string Output);
