namespace APThermo.Execution.Calibration;

/// <summary>The existing transaction state that prevented publication, without a second policy decision.</summary>
internal enum CalibrationNonpublication
{
    None,
    UnsupportedIdentity,
    IncompleteCoverage,
    InvalidObservation,
    OverTarget,
    NoUsefulPrefix,
}
