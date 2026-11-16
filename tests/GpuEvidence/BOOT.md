# BOOT.md — GPU evidence

## Purpose

Discover the release GPU selection from the SDK test adapter and verify both fresh TRX phases.
The discovered CUDA marker contract is described by [Execution.Tests](../Execution.Tests/API.md); this tool reads evidence, not its types.

## Invariants

An error callback or absent or repeated completion callback fails the inventory even when the adapter reports zero tests; a successful project with zero selected cases remains neutral. Discovery retains each test ID and display name, categories and CUDA evidence trait. The verifier requires an exact multiset and passing results, a nonempty remainder and the original sweep pair. It refuses absent, duplicated, incomplete, extra or malformed evidence and checks completed TRX summaries and consistent passing counters. Every CUDA case must report the scope declared by its discovered trait; hardware cases require a real CUDA marker and device, driver-only cases require an executed scenario without a CUDA claim. Refusal and execution cannot both be asserted; field types and nullable values are checked. Individual projects may have no selected tests.

## Dependencies

None

Outside the tree: the SDK test-platform translation layer and object model selected by global.json, and the solution's xUnit adapter.

## Constraints

The existing phase commands, selection, order, time budgets and throughput checks remain unchanged. Runs use .NET only on self-hosted machines. Discovery executes no test. The output inventory and TRX are retained as evidence.

## Acceptance criteria

- [ ] Windows and Linux actual release GPU jobs supply complete passing inventory and evidence at the final runtime commit.
- [x] Controlled valid, missing, duplicate, unexpected, truncated and marker-refusal cases pass or fail as appropriate (2026-11-16, `test_gpu_evidence.py`).
- [x] Native Windows and Linux fact/theory identity and trait probe matched TRX (2026-11-16, owner-held `fix-evidence/root/linux-discovery-poc-receipt.json` and Windows `discovery-win.out`); actual release discovery remains an independent acceptance condition.

## Taboos

- No Python, PowerShell 7 or external dependency on self-hosted runners.
- No GPU work, calibration or mutation during discovery.
