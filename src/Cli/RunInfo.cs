using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The run section of an output document.</summary>
internal sealed record RunInfo(string Command, IReadOnlyList<string> Inputs, DatabaseInfo? Database, AcceleratorInfo? Accelerator, Timings Timings, RunLimits Limits);
