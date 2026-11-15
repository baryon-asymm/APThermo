namespace APThermo.Execution.Calibration;

/// <summary>One immutable value snapshot, without workload references or cached results/timings.</summary>
internal readonly record struct CalibrationObservation
{
    public long TransactionId { get; init; }
    public long PreviousTransactionId { get; init; }
    public CalibrationOutcome PreviousOutcome { get; init; }
    public CalibrationNonpublication PreviousNonpublicationReason { get; init; }
    public long PreviousPublicationId { get; init; }
    public long PublicationId { get; init; }
    public long PreviousGeneration { get; init; }
    public long BeginGeneration { get; init; }
    public long CurrentGeneration { get; init; }
    public bool PreviousEntryPresent { get; init; }
    public int PriorPrefixCases { get; init; }
    public CalibrationReason MatchReason { get; init; }
    public CalibrationChoice BeforeUpload { get; init; }
    public CalibrationChoice AfterUpload { get; init; }
    public CalibrationChoice PhysicalFirstLaunch { get; init; }
    public CalibrationNonpublication NonpublicationReason { get; init; }
    public CalibrationOutcome Outcome { get; init; }
}
