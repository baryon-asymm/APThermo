using APThermo.Execution;

namespace APThermo.Cli;

/// <summary>
/// The run section of an output document. <see cref="Limits"/> is null for a command that takes neither
/// <c>--threshold</c> nor <c>--mass-tolerance</c> (<c>species</c>; the second hidden-defect audit of 2026-09-28,
/// observation 8): the `run` section then records only what the command actually took, instead of the two options'
/// defaults as if the caller had asked for them.
/// </summary>
internal sealed record RunInfo(string Command, IReadOnlyList<string> Inputs, DatabaseInfo? Database, AcceleratorInfo? Accelerator, Timings Timings, RunLimits? Limits);
