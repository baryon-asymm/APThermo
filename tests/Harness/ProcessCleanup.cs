namespace APThermo.Harness;

/// <summary>Bounded cleanup diagnostics, with secondary failures kept separate from the process interruption.</summary>
internal sealed record ProcessCleanup(string Description, Exception? Failure);
