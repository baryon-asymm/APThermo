namespace APThermo.Execution.Chunks;

/// <summary>One launch's slice of a batch: the first case and how many cases follow it.</summary>
internal readonly record struct Chunk(int Offset, int Length);
