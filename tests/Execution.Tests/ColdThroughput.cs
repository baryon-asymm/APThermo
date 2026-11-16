using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Checks the first cold sweep observation against its independent record.</summary>
internal static class ColdThroughput
{
    public static string? Violation(CudaTiming timing, int cases, long steps, IReadOnlyDictionary<string, string> approved, string fileName)
    {
        if (timing.KernelRuns.Count == 0 || timing.LaunchRuns.Count == 0 || timing.CalibrationRuns.Count == 0 || cases <= 0 || steps <= 0)
        {
            return "the cold throughput sample or its observation is missing";
        }

        var launch = timing.LaunchRuns[0];
        var observation = timing.CalibrationRuns[0];
        return launch.LaunchCount <= 0 || launch.CoveredCases != cases || launch.FirstLaunchCases != 1 || launch.ReusedPrefix ||
            launch.Kernel != timing.KernelRuns[0] || observation.TransactionId <= 0 || observation.PhysicalFirstLaunch.Cases != 1 ||
            observation.PhysicalFirstLaunch.Reason is CalibrationReason.NotChecked or CalibrationReason.ReusedPrefix
            ? "the first timed CUDA sample is not an observed cold one-case launch with complete coverage"
            : ThroughputRecord.Violation(approved, fileName, timing.KernelRuns[0].TotalSeconds / steps);
    }
}
