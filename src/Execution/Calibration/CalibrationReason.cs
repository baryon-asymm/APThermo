namespace APThermo.Execution.Calibration;

/// <summary>The actual short-circuit identity or first-choice disposition.</summary>
internal enum CalibrationReason
{
    NotChecked,
    NoPreviousEntry,
    UnsupportedIdentity,
    IdentityMismatch,
    DeadLauncher,
    LauncherMismatch,
    DeadTables,
    TablesMismatch,
    Matched,
    ReusedPrefix,
    Expired,
    BackwardClock,
    Superseded,
    Finished,
}
