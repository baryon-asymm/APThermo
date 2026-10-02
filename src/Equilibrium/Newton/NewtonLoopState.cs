namespace APThermo.Equilibrium.Newton;

/// <summary>
/// The verdict of <see cref="ConvergenceTests.Evaluate"/>: the small result the Newton loop reads instead of the tests'
/// own intermediate quantities (BOOT.md, ## Structure, "The Newton loop holds no formula").
/// </summary>
internal enum ConvergenceVerdict
{
    /// <summary>Equations (3.5) or (3.6) or the element balance are not yet met.</summary>
    NotConverged,

    /// <summary>The report's tests are met; the corrections have not yet reached the polish test's rounding floor.</summary>
    ReportTestsMet,

    /// <summary>The report's tests are met and the corrections are at rounding level: no further polish step is needed.</summary>
    Polished,
}

/// <summary>
/// The Newton loop's bookkeeping between steps (2026-09-26): how many steps have run since the condensed set last
/// changed, whether the most recent verdict passed the report's tests, and how many polish steps have followed
/// since. A struct rather than three loop locals so its transitions can be unit-tested on the host without a table
/// (BOOT.md, the loop's bookkeeping). Kernel-compatible.
/// </summary>
internal struct NewtonLoopState
{
    /// <summary>Newton steps taken since the condensed set last changed.</summary>
    public int Steps;

    /// <summary>Whether the most recent verdict passed the report's tests (equations (3.5) and (3.6), the element balance).</summary>
    public bool Converged;

    /// <summary>Polish steps taken in the current unbroken run of passed verdicts.</summary>
    public int PolishSteps;

    /// <summary>Vanished-species resets tried in the current <c>Converge</c> call before a condensed removal is attempted (RP-1311 section 3.6).</summary>
    public int SingularResets;

    /// <summary>
    /// A change of the condensed set — an inclusion, a phase change, or a singular remedy's removal — restarts the
    /// step count, since the cap is "steps after the last change of the condensed species set" (BOOT.md).
    /// </summary>
    public void RecordSetChange() => Steps = 0;

    /// <summary>
    /// One step's verdict: a failed one clears the mark and the polish count, so a later pass polishes afresh and a
    /// step cap reached after a failure is never reported as converged (BOOT.md, the loop's bookkeeping).
    /// </summary>
    public void RecordVerdict(ConvergenceVerdict verdict)
    {
        if (verdict == ConvergenceVerdict.NotConverged)
        {
            Converged = false;
            PolishSteps = 0;
            return;
        }

        Converged = true;
        if (verdict == ConvergenceVerdict.ReportTestsMet)
        {
            PolishSteps++;
        }
    }
}
