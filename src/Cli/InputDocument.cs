using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>A rocket or equilibrium problem document, read and validated.</summary>
internal sealed record InputDocument(PropellantDocument Propellant, ProblemDocument Problem, SweepDocument? Sweep, AcceleratorKind? Accelerator);
