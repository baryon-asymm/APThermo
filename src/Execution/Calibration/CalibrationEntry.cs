namespace APThermo.Execution.Calibration;

/// <summary>A prepared constant-size observation; publication adds no retained workload reference.</summary>
internal sealed class CalibrationEntry(CalibrationKey key)
{
    internal CalibrationKey Key { get; } = key;
    internal long PublicationId { get; set; }
    internal int PrefixCases { get; set; }
    internal long CompletedAt { get; set; }
}