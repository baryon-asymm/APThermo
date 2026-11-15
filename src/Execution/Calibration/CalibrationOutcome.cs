namespace APThermo.Execution.Calibration;

/// <summary>Publication is separate from the first successful physical launch decision.</summary>
internal enum CalibrationOutcome
{
    NotApplicable,
    Pending,
    Published,
    NotPublished,
    Abandoned,
    Invalidated,
    Superseded,
}
