# Troubleshooting

## Purpose

What to check when a run does not do what you expected: command-line exit codes,
per-case and per-station statuses, mass-tolerance refusals, and an accelerator that
was not found.

## When to use

- The command line exits with a non-zero code.
- A `Status` on a result or a station is not `Ok`.
- A mixture, propellant or state record is refused before any solve runs.
- CUDA was not used when you expected it, or `--accelerator cuda` refuses outright.

## Steps

**Read `Status` before reading a figure, always.** Numerical code never throws for a
per-case failure: it reports a status, and the failed result or station carries a
zero state instead of a partial one. This scenario shows every refusal a consumer
must be ready for — four exceptions and one failed station:

<!-- snippet: FailuresUsings -->
```csharp
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
```

`output` is any `TextWriter` (`Console.Out` in a console application):

<!-- snippet: Failures -->
```csharp
var database = SpeciesDatabase.LoadBundled();
using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

try
{
    Propellant.From(database).Oxidizer("NOT-A-REACTANT").Fuel("H2(L)").Build();
}
catch (KeyNotFoundException exception)
{
    output.WriteLine($"unknown reactant: {exception.Message}");
}

try
{
    Propellant.From(database).Named("O2(L)", massFraction: 1.0).OxidizerToFuelRatio(6.0).Build();
}
catch (ArgumentException exception)
{
    output.WriteLine($"named reactant with a ratio: {exception.Message}");
}

var composition = new Dictionary<string, double> { ["H"] = 141.73179242528607, ["O"] = 53.57343757533765 };   // ~1 kg
try
{
    var record = new StateRecord(Pressure: 1.0e6, Composition: composition, Temperature: 3000.0, Enthalpy: -1.0e6);
    solver.SolveStates([record]);
}
catch (StateRecordException exception)
{
    output.WriteLine($"state record refused: {exception.Reason}");
}

var doubled = composition.ToDictionary(kv => kv.Key, kv => 2.0 * kv.Value);   // ~2 kg: too heavy
try
{
    var mixture = ElementalMixture.Create(doubled, enthalpy: -1.0e6);
    solver.Solve(mixture, new RocketProblem { ChamberPressure = 1.0e6, AreaRatios = [10.0] });
}
catch (MixtureMassException exception)
{
    output.WriteLine($"mixture too heavy: {exception.Reason}");
}

var propellant = Propellant.From(database)
    .Oxidizer("O2(L)", temperature: 90.17)
    .Fuel("H2(L)", temperature: 20.27)
    .OxidizerToFuelRatio(6.0)
    .Build();
var result = solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, PressureRatios = [0.5] });
Station exit = result.Stations[^1];
output.WriteLine($"a failed station carries a status instead of a partial state: {exit.Status}");
```

## Errors

Command-line exit codes:

| Code | Meaning |
|---|---|
| 0 | Every case and station solved successfully. |
| 1 | At least one case or station failed numerically; the document is still written with a per-case and per-station `status`. |
| 2 | Invalid input document, unknown option, bad database path, unknown reactant, a mass-tolerance refusal, or an unknown schema name. A message on standard error names the problem. |
| 3 | Accelerator unavailable, or an unexpected infrastructure failure. Every library and device path tried is named. |

Per-case `CaseStatus` values, read off a `Station`, a `RocketResult` or an
`EquilibriumResult`:

| Status | Meaning |
|---|---|
| `Ok` | Solved; every figure is meaningful. |
| `InvalidInput` | Every element abundance zero, a negative abundance, an empty table, a non-positive pressure, or (tp) a non-positive temperature. |
| `NotConverged` | The Newton iteration did not meet its tests within the step budget, or the element-conservation check failed at the end. |
| `SingularMatrix` | The derivative matrix stayed singular after the reference's remedies. |
| `TemperatureOutOfRange` | An hp or sp iterate left the database's temperature range `[100 K, 20000 K]`. This is a status, not an exception: the solve does not throw for it. |

On any status but `Ok`, the state is zero, not partial — check `Status` first.

Refusals raised before any kernel runs (exceptions, not statuses):

| Situation | Exception |
|---|---|
| Unknown reactant name | `KeyNotFoundException`, at `Build` |
| Invalid mixture rule (a ratio without an oxidizer and a fuel, oxidizers and fuels without a ratio, a named reactant with a ratio), a reactant temperature out of range, a non-positive pressure or chamber pressure | `ArgumentException`, at `Build` or `Solve` |
| A state record with none or more than one target, exits without an enthalpy, a flow named without exits, a record given to the wrong batch method | `StateRecordException`, naming the record's index and reason |
| A mixture's element moles do not weigh one kilogram with the database's atomic weights, within the tolerance in force (1 % by default, `--mass-tolerance` on the command line, `ElementalMixture.MassTolerance` in C#) | `MixtureMassException`, naming the mixture, the mass found and the tolerance |
| The accelerator requested cannot be bound (see [GPU acceleration](gpu.md)) | `AcceleratorUnavailableException`, naming every path tried |

## See also

- [Command line](cli.md) — every command and its exit codes
- [Rocket solving](rocket.md) — refusals specific to building a propellant
- [State records](states.md) — the mass tolerance and the record-shape rules in context
- [GPU acceleration](gpu.md) — diagnosing an accelerator that was not found
