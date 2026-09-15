namespace AerospacePropellantThermodynamics.Execution;

/// <summary>The phases of one run that <see cref="RunTimings"/> reports separately.</summary>
internal enum RunPhase
{
    /// <summary>Host to device, and the clearing of the outputs a kernel accumulates into.</summary>
    Upload,

    /// <summary>The launch and the synchronisation that follows it.</summary>
    Kernel,

    /// <summary>Device to host.</summary>
    Download,
}
