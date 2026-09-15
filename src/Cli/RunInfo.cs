using APThermo.Execution;

namespace APThermo.Cli;

/// <summary>The run section of an output document.</summary>
internal sealed record RunInfo(string Command, IReadOnlyList<string> Inputs, DatabaseInfo? Database, AcceleratorInfo? Accelerator, Timings Timings, RunLimits Limits);
