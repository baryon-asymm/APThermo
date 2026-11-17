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
zero state, except that `NoGasPhase` retains temperature and pressure. Compositions
at other failed stations may contain diagnostic iterates, not a solution. This
scenario shows four of the five refusals of
the exception table below (the accelerator refusal is covered in
[GPU acceleration](gpu.md)) and one failed station:

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
    _ = Propellant.From(database).Oxidizer("NOT-A-REACTANT").Fuel("H2(L)").Build();
}
catch (KeyNotFoundException exception)
{
    output.WriteLine($"unknown reactant: {exception.Message}");
}

try
{
    _ = Propellant.From(database)
        .Oxidizer("O2(L)")
        .Fuel("H2(L)")
        .Named("AL(cr)", massFraction: 0.1)
        .OxidizerToFuelRatio(6.0)
        .Build();
}
catch (ArgumentException exception)
{
    output.WriteLine($"named reactant with a ratio: {exception.Message}");
}

var composition = new Dictionary<string, double> { ["H"] = 141.73179242528607, ["O"] = 53.57343757533765 };   // ~1 kg
try
{
    var record = new StateRecord(Pressure: 1.0e6, Composition: composition, Temperature: 3000.0, Enthalpy: -1.0e6);
    _ = solver.SolveStates([record]);
}
catch (StateRecordException exception)
{
    output.WriteLine($"state record refused: {exception.Reason}");
}

var doubled = composition.ToDictionary(kv => kv.Key, kv => 2.0 * kv.Value);   // ~2 kg: too heavy
try
{
    var mixture = ElementalMixture.Create(doubled, enthalpy: -1.0e6);
    _ = solver.Solve(mixture, new RocketProblem { ChamberPressure = 1.0e6, AreaRatios = [10.0] });
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
var exit = result.Stations[^1];
output.WriteLine($"a failed station carries a status instead of a partial state: {exit.Status}");
```

## Errors

Command-line exit codes:

| Code | Meaning |
|---|---|
| 0 | Every case and station solved successfully. |
| 1 | At least one case or station failed numerically or ended `noGasPhase`; the document is still written with a per-case and per-station `status`. |
| 2 | Invalid input document, unknown option, bad database path, unknown reactant, a mass-tolerance refusal, or an unknown schema name. A message on standard error names the problem. |
| 3 | Accelerator unavailable, or an unexpected infrastructure failure. The diagnostic reports the cause available; it need not enumerate every library or device path. |

Per-case `CaseStatus` values, read off a `Station`, a `RocketResult` or an
`EquilibriumResult`:

| Status | Meaning |
|---|---|
| `Ok` | Converged within the selected model and its numerical checks. Read the model-applicability limits below before interpreting the figures. |
| `InvalidInput` | An equilibrium solve refused before iterating: every element abundance zero, a negative abundance, an empty table, a non-positive pressure, or (tp) a non-positive temperature. A rocket station refuses the same way for a non-positive chamber pressure, an empty table, or a pressure ratio not above 1 for that station; the exits after a failed chamber or throat keep this status instead of solving further. Transport refuses it for a non-positive or NaN temperature, a negative or NaN mole number, or a transport table that does not match the species table. |
| `NotConverged` | The Newton iteration did not meet its tests within the step budget, the condensed set changed too many times, or the element-conservation check failed at the end; for a rocket exit, an area ratio not met within its own step budget. |
| `SingularMatrix` | The derivative matrix stayed singular after the available remedies. If the transport reaction system fails, `TransportStatus` records the refusal and the public station's `Transport` is null; an internal frozen fallback is not a public successful transport result. |
| `TemperatureOutOfRange` | A temperature iterate or a final state failed the numerical window, fit or finite-property checks; TP is subject to final-state checks too. HP/SP recovery may reach a search boundary without finding an accepted target. The status is a numerical refusal, not a proof that no physical equilibrium exists. The exact windows are in the [equilibrium contract](../../src/Equilibrium/API.md). |
| `ThroatNotFound` | The search could not establish the first mass-flux maximum from the chamber within its budget; the case ends there. At a phase boundary this need not be a Mach-one point. |
| `AreaRatioInvalid` | A rocket exit's area ratio was not above 1. |
| `NoTransportData` | No gaseous species carried positive moles, so transport has nothing to evaluate. |
| `NoGasPhase` | The equilibrium holds no gas phase (condensed species alone), proven by a tangent-plane certificate. `Moles` hold the condensed minimum with every gas zero. The state carries the temperature and the pressure only, every other field is 0; for an hp or sp case the temperature is the one the search found. This is a result, not a failure of the model: the command line writes `noGasPhase` and exits 1. |

On any status but `Ok`, the state is zero, not partial — check `Status` first. The one exception is `NoGasPhase`, whose state is the temperature and the pressure.

A species list and its admitted phase ranges define the model. This applies to the
default database selection as well as to `Only`: some condensed records cease to be
candidates below their lower effective bound. For example, graphite is absent below
300 K, so a C/H TP result can change sharply across that boundary and may represent
supersaturated gas. This is a range-limited equilibrium model, not evidence of the
stable state of the real material. Effective/open-below rules also exist: ice can
remain eligible down to 160 K despite a raw fit starting at 200 K. The library adds
no species to an `Only` list. See [model applicability](../../src/Problems/API.md#model-applicability).

Frozen flow retains the freezing station's condensed composition. Its properties
may therefore extrapolate a condensed fit far outside that fit's temperature range;
an `Ok` station does not certify the extrapolation's physical accuracy. Before using
such a result, inspect its positive condensed mass fractions and the corresponding
records from `SpeciesDatabase.Records`, and compare the station temperature with
their raw fit bounds. The aluminized custom-propellant example uses this policy.
For applications that require equilibrium phase changes, use shifting equilibrium
and still check its admitted phase ranges. Both frozen and reacting transport figures
may be reported on a frozen station; choose the transport model appropriate to the
application rather than interpreting both as the same property.

Refusals raised before any kernel runs (exceptions, not statuses):

| Situation | Exception |
|---|---|
| Unknown reactant name | `KeyNotFoundException`, at `Build` |
| Invalid mixture rule (a ratio without an oxidizer and a fuel, oxidizers and fuels without a ratio, a named reactant with a ratio), a reactant temperature out of range, a non-positive pressure or chamber pressure | `ArgumentException`, at `Build` or `Solve` |
| A state record with none or more than one target, exits without an enthalpy, a flow named without exits, a record given to the wrong batch method | `StateRecordException`, naming the record's index and reason |
| A mixture's element moles do not weigh one kilogram with the database's atomic weights, within the tolerance in force (1 % by default, `--mass-tolerance` on the command line; in C#, the `massTolerance` argument of `ElementalMixture.Create` or `StateBatchOptions.MassTolerance`, read back afterwards from the mixture's own get-only `MassTolerance`) | `MixtureMassException`, naming the mixture, the mass found and the tolerance |
| The accelerator requested cannot be bound (see [GPU acceleration](gpu.md)) | `AcceleratorUnavailableException` with the reported reason |

## See also

- [Command line](cli.md) — every command and its exit codes
- [Rocket solving](rocket.md) — refusals specific to building a propellant
- [State records](states.md) — the mass tolerance and the record-shape rules in context
- [GPU acceleration](gpu.md) — diagnosing an accelerator that was not found
