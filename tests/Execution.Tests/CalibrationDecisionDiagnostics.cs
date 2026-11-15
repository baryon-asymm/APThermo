using System.Globalization;
using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Bounded post-measurement diagnostic text, without a new acceptance threshold.</summary>
internal static class CalibrationDecisionDiagnostics
{
    internal static string Describe(CalibrationObservation observation) =>
        $"transaction={observation.TransactionId}; previousTransaction={observation.PreviousTransactionId}; " +
        $"previousOutcome={observation.PreviousOutcome}/{observation.PreviousNonpublicationReason}; " +
        $"priorEntry={observation.PreviousEntryPresent}; previousPublication={observation.PreviousPublicationId}; " +
        $"priorPrefix={observation.PriorPrefixCases}; match={observation.MatchReason}; " +
        $"generation(previous/begin/current)={observation.PreviousGeneration}/{observation.BeginGeneration}/{observation.CurrentGeneration}; " +
        $"beforeUpload=[{Describe(observation.BeforeUpload)}]; afterUpload=[{Describe(observation.AfterUpload)}]; " +
        $"physicalFirst=[{Describe(observation.PhysicalFirstLaunch)}]; " +
        $"outcome={observation.Outcome}/{observation.NonpublicationReason}; publication={observation.PublicationId}";

    private static string Describe(CalibrationChoice choice) =>
        $"cases={choice.Cases}, reason={choice.Reason}, ageTicks=" +
        (choice.CandidateAge is { } age ? age.Ticks.ToString(CultureInfo.InvariantCulture) : "unavailable");
}
