using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Verifies the observed calibration modes of the timed sweep.</summary>
internal static class SweepMode
{
    private const int Samples = 3;

    public static string? Violation(CudaTiming timing)
    {
        if (timing.KernelRuns.Count != Samples || timing.LaunchRuns.Count != Samples || timing.CalibrationRuns.Count != Samples)
        {
            return $"expected exactly {Samples} timed, launch and calibration observations; " +
                $"found {timing.KernelRuns.Count}, {timing.LaunchRuns.Count} and {timing.CalibrationRuns.Count}";
        }

        for (var index = 0; index < Samples; index++)
        {
            var launch = timing.LaunchRuns[index];
            var observation = timing.CalibrationRuns[index];
            var choice = observation.PhysicalFirstLaunch;
            var validLaunch = launch.LaunchCount > 0 && launch.CoveredCases == SweepRun.LongRunningCases &&
                launch.Kernel == timing.KernelRuns[index] && observation.TransactionId > 0;
            var expectedMode = index == 0
                ? !launch.ReusedPrefix && launch.FirstLaunchCases == 1 && choice.Cases == 1 &&
                  choice.Reason is not (CalibrationReason.NotChecked or CalibrationReason.ReusedPrefix)
                : launch.ReusedPrefix && launch.FirstLaunchCases > 0 && choice.Cases == launch.FirstLaunchCases &&
                  choice.Reason == CalibrationReason.ReusedPrefix;
            if (!validLaunch || !expectedMode)
            {
                return $"CUDA timed sample {index + 1} expected {(index == 0 ? "cold" : "reused prefix")}; " +
                    $"{LaunchBudgetObservation.Describe(launch)}; {CalibrationDecisionDiagnostics.Describe(observation)}";
            }
        }

        return null;
    }
}
