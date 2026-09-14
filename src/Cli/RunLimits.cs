namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The run-wide options that describe the caller's records rather than the physics: the composition threshold and the mass tolerance.</summary>
internal sealed record RunLimits(double Threshold, double MassTolerance);
