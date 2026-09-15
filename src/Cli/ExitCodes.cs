using AerospacePropellantThermodynamics.Cli.Cases;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>0 when every case, station and transport evaluation is Ok, else 1 (F-CL-10).</summary>
internal static class ExitCodes
{
    public static ExitCode Of(IReadOnlyList<CaseOutput> cases)
    {
        var failed = cases.Any(c => c.Status != CaseStatus.Ok
                                    || c.Stations.Any(s => s.Status != CaseStatus.Ok || (s.TransportStatus is { } t && t != CaseStatus.Ok)));
        return failed ? ExitCode.CaseFailed : ExitCode.Ok;
    }
}
