namespace APThermo.Cli;

/// <summary>
/// The two run parameters the <c>run</c> section records beside the timings: the mole-fraction threshold of the
/// compositions, a presentation choice, and the mass tolerance declared for mixtures built from element moles, a
/// description of the caller's records (API.md, Command line); neither is a field of the documents.
/// </summary>
internal sealed record RunLimits(double Threshold, double MassTolerance);
