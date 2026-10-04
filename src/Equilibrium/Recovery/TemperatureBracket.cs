using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// The temperature bracket of an hp or sp case the report's own attempts could not solve (BOOT.md, "The bracket"): the
/// assigned temperature of tp equilibria is iterated until two of them bracket the assigned enthalpy or entropy within a
/// narrow interval. Safeguarded Newton on ln T with dh/dln T = T cp_eq and ds/dln T = cp_eq (the condensed minimum's own
/// slope where the probe holds no gas), bisection where the derivative is missing (the plateau convention's zero) or the
/// step leaves the bracket or fails to halve it. This struct holds the ends, the steps and the pure transitions, and touches
/// no view: the compositions of the ends are <see cref="BracketSeeds"/>'. Kernel-compatible; testable on the host.
/// </summary>
internal struct TemperatureBracket
{
    private const int MaxProbes = 40;

    private const int MaxRetreats = 8;

    /// <summary>ln 2: a one-sided step changes T by at most a factor of two.</summary>
    private const double MaxLogStep = 0.6931471805599453;

    /// <summary>The bracket, in ln T, below which two ends that are not both gasless seed the final attempt; and a gas probe's converged step.</summary>
    private const double Narrow = 1.0e-7;

    /// <summary>A gasless probe's converged step, in ln T: its slope is exact inside a condensed vertex, so Newton converges quadratically.</summary>
    private const double GaslessNarrow = 1.0e-12;

    /// <summary>The bracket, in ln T, below which two gasless ends end the search: the range rule's relative tolerance.</summary>
    private const double GaslessGap = 1.0e-9;

    /// <summary>K, section 3.1's estimate: the first probe when the case gives none.</summary>
    private const double DefaultStart = 3800.0;

    /// <summary>
    /// A scan probe stands this far (relative) below a dead-end floor: outside the range tolerance of its record
    /// (<c>PhaseGeometry</c>'s 1e-9), so that the probe is the far side of the floor.
    /// </summary>
    private const double ScanMargin = 1.0e-8;

    /// <summary>Whether the bracket has started: the case's own attempts failed.</summary>
    public bool Active;

    /// <summary>The status the case's own attempts ended with; reported when the bracket gives up without a verdict.</summary>
    public CaseStatus FirstFailure;

    /// <summary>The status the bracket gave up with.</summary>
    public CaseStatus GiveUpStatus;

    /// <summary>ln T of the lower end, whose tp equilibrium's assigned property lies below the target.</summary>
    public double LowX;

    /// <summary>ln T of the upper end, whose tp equilibrium's assigned property lies at or above the target.</summary>
    public double HighX;

    /// <summary>The assigned property (h in J/kg, s in J/(kg·K)) of the tp equilibrium at the lower end.</summary>
    public double LowP;

    /// <summary>The assigned property of the tp equilibrium at the upper end.</summary>
    public double HighP;

    // No two bool fields are adjacent below: the struct is carried through the case's loop, and ILGPU 1.5.3 loads two adjacent
    // bools as one vector into predicate registers, which ptxas refuses (BOOT.md, "No whole-struct copies").

    /// <summary>Whether a lower end is known.</summary>
    public bool HaveLow;

    /// <summary>What the lower end found.</summary>
    public EndKind LowKind;

    /// <summary>Whether an upper end is known.</summary>
    public bool HaveHigh;

    /// <summary>What the upper end found.</summary>
    public EndKind HighKind;

    /// <summary>Whether the last recorded probe was stored as the lower end.</summary>
    public bool LastBelow;

    /// <summary>What the last recorded probe found.</summary>
    public EndKind LastKind;

    /// <summary>ln T of the probe in flight.</summary>
    public double ProbeX;

    /// <summary>Whether the search is scanning the far sides of the dead-end floors below a gap for the first probe above the target (BOOT.md, "Dead-end gaps").</summary>
    public bool Scanning;

    /// <summary>Probes recorded.</summary>
    public int Probes;

    /// <summary>Failed probes answered by a retreat.</summary>
    public int Retreats;

    /// <summary>The step on ln T before the last one, for the bisection safeguard.</summary>
    public double OlderStep;

    /// <summary>The last step on ln T.</summary>
    public double LastStep;

    /// <summary>Whether the pass in flight is the verdict-only final of two gasless ends or of a converged gasless probe.</summary>
    public bool Finishing;

    /// <summary>The move that ended the search, once a gasless final is pending.</summary>
    public BracketMove Final;

    /// <summary>ln T of a gasless final taken at the temperature Newton's step converged on.</summary>
    public double FinalX;

    /// <summary>The temperature of the probe in flight, K, never below the mixture window's floor.</summary>
    public readonly double ProbeTemperature => KernelMath.Max(EquilibriumSolver.MinMixtureTemperature, Math.Exp(ProbeX));

    /// <summary>Whether the search stands at a one-sided probe going down, where the data floor of a condensed record may stop it.</summary>
    public readonly bool NeedsFloor => !(HaveLow && HaveHigh) && !LastBelow;

    /// <summary>Starts the search at <paramref name="estimate"/> K, or at section 3.1's 3800 K when none is given, remembering the failure that sent the case here.</summary>
    public void Start(CaseStatus firstFailure, double estimate)
    {
        Active = true;
        FirstFailure = firstFailure;
        ProbeX = Math.Log(estimate > 0.0 ? estimate : DefaultStart);
    }

    /// <summary>Whether the end nearer to <paramref name="x"/> is the lower one; false when only the upper end is known.</summary>
    public readonly bool NearerIsLow(double x) => HaveLow && (!HaveHigh || x - LowX <= HighX - x);

    /// <summary>What the nearer end to the probe found.</summary>
    public readonly EndKind NearerKind => NearerIsLow(ProbeX) ? LowKind : HighKind;

    /// <summary>
    /// Stores the probe as the end of its side (the lower when <paramref name="value"/> lies below <paramref name="target"/>)
    /// and returns the Newton step on ln T: <c>(target − value)/slope</c>, or ±ln 2 toward the target where the slope is not
    /// positive. The caller saves the probe's moles in the end's slot (<see cref="LastBelow"/>).
    /// </summary>
    public double Record(double target, double value, double slope, EndKind kind)
    {
        var below = value < target;
        if (below)
        {
            LowX = ProbeX;
            LowP = value;
            HaveLow = true;
            LowKind = kind;
        }
        else
        {
            HighX = ProbeX;
            HighP = value;
            HaveHigh = true;
            HighKind = kind;
        }

        Scanning = false;
        LastBelow = below;
        LastKind = kind;
        Probes++;
        return slope > 0.0 ? (target - value) / slope : (below ? MaxLogStep : -MaxLogStep);
    }

    /// <summary>
    /// What to do after the probe just recorded, whose Newton step is <paramref name="step"/>: finish (the bracket is narrow, or
    /// Newton converged), give up (the probes are spent or the domain's edge reached), or move the probe. When it moves,
    /// <paramref name="floor"/> is the temperature of the data floor that stops a downward step (0 when none).
    /// </summary>
    public BracketMove Advance(double step, double floor)
    {
        var both = HaveLow && HaveHigh;
        if (both && HighX - LowX <= (LowKind == EndKind.Gasless && HighKind == EndKind.Gasless ? GaslessGap : Narrow))
        {
            return LeverFinal();
        }

        var gasless = LastKind == EndKind.Gasless;
        if (Math.Abs(step) <= (gasless ? GaslessNarrow : Narrow))
        {
            FinalX = ProbeX + step;
            return gasless ? BracketMove.GaslessAtProbe : BracketMove.AttemptFromProbe;
        }

        if (Probes >= MaxProbes)
        {
            return both ? LeverFinal() : GiveUp(FirstFailure);
        }

        if (!both && OutOfDomain())
        {
            return GiveUp(CaseStatus.TemperatureOutOfRange);
        }

        var next = both ? Inside(step) : Outward(step, floor);
        OlderStep = LastStep;
        LastStep = next - ProbeX;
        ProbeX = next;
        return BracketMove.Probe;
    }

    /// <summary>
    /// The final attempt of two narrow ends has failed on a gap at a dead-end bound, so no state lies between them (BOOT.md,
    /// "Dead-end gaps"): the ends are dropped and the search scans the far sides of the floors below, keeping the first failure.
    /// </summary>
    public void BeginScan()
    {
        var failure = FirstFailure;
        this = default;
        Active = true;
        FirstFailure = failure;
        Scanning = true;
    }

    /// <summary>
    /// The next scan probe, just below <paramref name="floor"/> (the highest dead-end floor below the last one, 0 when there
    /// is none). With no floor left the case gives up <c>TemperatureOutOfRange</c>: the segment below the last probe holds no
    /// state of the target either.
    /// </summary>
    public BracketMove Scan(double floor)
    {
        if (!(floor > 0.0))
        {
            return GiveUp(CaseStatus.TemperatureOutOfRange);
        }

        ProbeX = Math.Log(floor * (1.0 - ScanMargin));
        return BracketMove.Probe;
    }

    /// <summary>A failed probe retreats halfway toward the nearer known end, a bounded number of times; with no end known, or the retreats spent, the case gives up with its first failure.</summary>
    public BracketMove Retreat()
    {
        if (!(HaveLow || HaveHigh) || Retreats >= MaxRetreats)
        {
            return GiveUp(FirstFailure);
        }

        Retreats++;
        var toward = NearerIsLow(ProbeX) ? LowX : HighX;
        ProbeX = 0.5 * (ProbeX + toward);
        return BracketMove.Probe;
    }

    /// <summary>The lever rule's fraction of the way from the lower end to the upper: <c>(target − P_low)/(P_high − P_low)</c> clamped to [0, 1], 0.5 when the difference is not positive.</summary>
    public readonly double LeverFraction(double target)
    {
        var span = HighP - LowP;
        return span > 0.0 ? KernelMath.Max(0.0, KernelMath.Min(1.0, (target - LowP) / span)) : 0.5;
    }

    /// <summary>The lever rule's temperature between the two ends, K.</summary>
    public readonly double LeverTemperature(double target) =>
        Math.Exp(LowX) + LeverFraction(target) * (Math.Exp(HighX) - Math.Exp(LowX));

    private BracketMove GiveUp(CaseStatus status)
    {
        GiveUpStatus = status;
        return BracketMove.GiveUp;
    }

    /// <summary>
    /// The final when both ends are known, chosen by what the ends found. Two gasless ends end in the verdict alone; any
    /// other pair, one gas end or two, in the hp or sp case itself seeded by the lever rule. The trace-gas design adds the
    /// arm of an end of its own kind here (BOOT.md, "The trace-gas seam" (b)).
    /// </summary>
    private readonly BracketMove LeverFinal() =>
        (LowKind, HighKind) switch
        {
            (EndKind.Gasless, EndKind.Gasless) => BracketMove.GaslessFromLever,
            _ => BracketMove.AttemptFromLever,
        };

    private readonly bool OutOfDomain() =>
        LastBelow ? ProbeX >= Math.Log(EquilibriumSolver.MaxTemperature) : ProbeX <= Math.Log(EquilibriumSolver.MinMixtureTemperature);

    /// <summary>Inside a known bracket: the Newton point, or the midpoint when it leaves the bracket or does not halve the step.</summary>
    private readonly double Inside(double step)
    {
        var next = ProbeX + step;
        var outside = !(next > LowX && next < HighX);
        var slow = Math.Abs(2.0 * step) > Math.Abs(OlderStep);
        return outside || slow ? 0.5 * (LowX + HighX) : next;
    }

    /// <summary>
    /// One side known: the Newton step limited to a factor of two and to the domain; going down, the probe stops at the
    /// data floor of a condensed record (<paramref name="floor"/>, K, 0 when none) before crossing it, since below a record's
    /// data the record is no candidate and the restricted equilibrium there may lie on the wrong side of the target (the
    /// data floor makes h and s of the tp equilibria non-monotone in T).
    /// </summary>
    private readonly double Outward(double step, double floor)
    {
        var next = ProbeX + KernelMath.Max(-MaxLogStep, KernelMath.Min(MaxLogStep, step));
        next = KernelMath.Max(Math.Log(EquilibriumSolver.MinMixtureTemperature), KernelMath.Min(Math.Log(EquilibriumSolver.MaxTemperature), next));
        return !LastBelow && floor > 0.0 ? KernelMath.Max(next, Math.Log(floor)) : next;
    }
}
