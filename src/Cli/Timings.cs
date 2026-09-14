namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The tool's own phases, seconds: the database load and the solve (which the front door does not expose).</summary>
internal sealed record Timings(double Database, double Solve);
