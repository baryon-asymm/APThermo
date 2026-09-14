namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Exit codes of the command line (BOOT.md, invariants).</summary>
public enum ExitCode
{
    /// <summary>Every case and station is Ok.</summary>
    Ok = 0,

    /// <summary>At least one case failed numerically; the document was still written.</summary>
    CaseFailed = 1,

    /// <summary>An invalid input document, option, database path or reactant; nothing was written.</summary>
    InvalidInput = 2,

    /// <summary>An accelerator or infrastructure error.</summary>
    Infrastructure = 3,
}
