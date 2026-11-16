# API.md — GPU evidence

## Command ✅

`dotnet run --project tests/GpuEvidence --configuration Release -- discover <inventory.json>` discovers solution tests without running them; set `GPU_EVIDENCE_SDK` to the version selected by `dotnet --version`. `verify <inventory.json> <remainder-trx-name> <sweep-trx-name>` compares fresh reports beneath `tests` to that inventory and validates CUDA execution markers. Exit zero means all checks passed; diagnostics go to stderr on refusal.

The executable exports no public .NET types.
