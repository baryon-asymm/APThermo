namespace AerospacePropellantThermodynamics.Cli;

internal sealed record DatabaseInfo(string ThermoPath, string? TransPath, string ThermoSha256, string? TransSha256);
