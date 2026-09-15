using APThermo.Execution;
using APThermo.Problems;

namespace APThermo.Cli.Syntax;

/// <summary>The options of one invocation, after parsing; every command checks that the options given apply to it.</summary>
internal sealed record CommandOptions
{
    /// <summary>The reference's print threshold on mole fractions (Fixtures BOOT.md).</summary>
    public const double DefaultThreshold = 5e-6;

    public string? Output { get; init; }

    public OutputFormat Format { get; init; } = OutputFormat.Json;

    public AcceleratorKind? Accelerator { get; init; }

    public string? Database { get; init; }

    public double Threshold { get; init; } = DefaultThreshold;

    /// <summary>The mass tolerance declared for every mixture built from element moles (the Problems BOOT.md); a propellant by reactants keeps the library's default.</summary>
    public double MassTolerance { get; init; } = ElementalMixture.DefaultMassTolerance;

    public bool Transport { get; init; }

    public string? Find { get; init; }

    /// <summary>The option names given, for the per-command applicability check.</summary>
    public IReadOnlySet<string> Given { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}
