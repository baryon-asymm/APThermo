namespace APThermo.Execution.Tests;

/// <summary>
/// Where <see cref="ComparisonSupport"/> gets the species functions of a table from: <paramref name="Cpu"/> evaluates them on the CPU
/// accelerator, <paramref name="Other"/> on the accelerator the CPU's results are compared with.
/// </summary>
/// <param name="Cpu">The evaluation on the CPU accelerator.</param>
/// <param name="Other">The evaluation on the other accelerator.</param>
internal sealed record SpeciesFunctionSources(Func<SpeciesFunctionBatch, SpeciesFunctionBatchResult> Cpu,
                                              Func<SpeciesFunctionBatch, SpeciesFunctionBatchResult> Other);
