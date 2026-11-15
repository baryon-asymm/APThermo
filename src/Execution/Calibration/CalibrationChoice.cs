namespace APThermo.Execution.Calibration;

/// <summary>One staged choice; a missing candidate has no invented zero age.</summary>
internal readonly record struct CalibrationChoice(int Cases, CalibrationReason Reason, TimeSpan? CandidateAge);
