# API.md — Execution.Calibration

## Calibration observation (tree contract) ⏳

An internal immutable value snapshot identifies the latest transaction and preceding
publication, identity match disposition, generation/normal consumption, candidate
prefix, staged first-choice reasons/nullable ages and completion/publication outcome.
The preceding terminal outcome is retained even if no reusable entry exists.
Nonpublication distinguishes unsupported identity, incomplete coverage, invalid or
over-target observation, no useful prefix, abandonment and superseding generation.
All ages use the transaction's existing monotonic provider and completion epoch;
they are not the full age of the earlier prefix measurement. Absence is not age zero.
The second upload check retains a prior expiry reason. First physical launch choice
is frozen on current successful synchronization; later publication cannot replace it.
The parent exposes a value copy for friend tests after Run has returned. No workload
identity object, array, buffer, delegate, result or RunTimings is retained in it.
Diagnostics do not change the existing policy or claim zero observation overhead.

Internal host-only contract for the parent pipelines. This project-less node
has no package surface. Implementation helpers remain private to this node.

## Run transaction ✅

```csharp
namespace APThermo.Execution.Calibration;

internal sealed class LaunchCalibrationCache
{
    public LaunchCalibrationCache(TimeProvider? clock = null);
    public CalibrationRun Begin<TViews>(Delegate launcher, UploadedTables tables, ChunkPlan plan,
        string? inputIdentity, in TViews views) where TViews : struct;
    public void Clear();
}

internal sealed class CalibrationRun : IDisposable
{
    public bool ReusedPrefix { get; }
    public int FirstChunkCases();
    public void ObserveSuccessfulLaunch(int cases, TimeSpan elapsed);
    public void Complete();
    public void Dispose();
}
```

Begin consumes the preceding entry and derives all supported scalar identity
from the actual views. The pre-allocation input identity comes from Chunks.
Unbounded plans retain their static size. Unsupported/mismatched/stale bounded
identities yield cold sampling.
FirstChunkCases rechecks monotonic freshness and respects the current plan.
ObserveSuccessfulLaunch receives the same interval R09 records, after sync and
before download. It retains no timings object, buffer, result or callback.
Complete is called only after successful cleanup and result assembly. Dispose
without Complete invalidates; both Dispose and Clear are idempotent and do not
replace an existing primary. A target overrun always prevents publication.

The entry's validity interval is one budget target from completion. Equality of
framed SHA-256 digests is practical content identity. Input arrays may not change
between capture and final upload; no post-download seed hash is used.

Cold bounded runs still start with one case. Known-prefix reuse is an empirical new probe
of previously observed work, not an assertion that its merged launch was measured.


ReusedPrefix reports the latest first-launch freshness decision. The parent records it only on the first CURRENT successful synchronized launch; no saved timing enters a result.
