# API.md — Cli

Namespace `AerospacePropellantThermodynamics.Cli`, tool command `apthermo`. The node
exposes a command line, an in-process entry point, and the JSON document shapes it
reads and writes. Everything not listed here is internal and may change.

## Command line ✅

```console
$ apthermo rocket problem.json [--output result.json] [--format json|csv] [--accelerator auto|cpu|cuda] [--database DIR] [--threshold 5e-6]
$ apthermo equilibrium problem.json [same options]
$ apthermo states records.json [more files...] [--output results.json] [--format json|csv] [--transport] [--accelerator auto|cpu|cuda] [--database DIR] [--threshold X]
$ apthermo species [--find TEXT] [--database DIR] [--output PATH] [--format json|csv]
$ apthermo devices [--output PATH]
$ apthermo --help
```

Options take their value as the next argument or after `=`; every option applies only
to the commands listed above it. `--database` names a directory with `thermo.inp`
and, optionally, `trans.inp`; without it the tool looks for `data/` next to the
executable, then `data/` under the current directory, then the current directory
itself. `--accelerator` overrides the document's `engine.accelerator`; the default is
`auto`. `--threshold` omits mole fractions below its value from the compositions
(default 5e-6, the reference's print threshold).

`states` takes state records, the exchange shape of the `Problems` node, as a JSON
array, a single object, JSON Lines (one record per line) or several such files, and
solves them as one batch per kind: the records without exits as one equilibrium
batch over the union of their elements, the records with exits as one rocket batch.
A state record is exactly this shape:

```json
{ "pressure": 6500000.0, "enthalpy": -1527829.408385985, "composition": { "C": 9.505849129331365, "H": 35.214695099119155, "O": 15.704786718374072, "N": 6.007718569653603, "Cl": 3.3293409533388547, "Al": 14.709996403305084 } }
```

with `pressure` in Pa, `composition` as element moles per kilogram of mixture (element
symbols in any case), and exactly one of `enthalpy` (J/kg, hp problem), `temperature`
(K, tp problem) or `entropy` (J/(kg·K), sp problem). Optional `areaRatios` or
`pressureRatios` on a record turn it into a rocket case with `pressure` as the chamber
pressure and `enthalpy` required; an optional `flow` names the flow model of such a
record. Unknown fields are an error, so that a unit mistake cannot pass silently, and
so is a composition that does not weigh one kilogram with the database's atomic
weights within the front door's tolerance (`ElementalMixture.MassTolerance`, 1 %): a
record in mol/g, in kmol/kg or per two kilograms is refused naming the record, the mass
found and the tolerance (the errors table below).

⚠ 2026-09-13: the example record stood with `"O": 31.2, "N": 4.1` at 1 MPa, an
illustration that weighed 706 g and would now be refused; it is the record of another
simulation that the mass check was written for (1000.015 g), and the tests node solves
every record example of this document.

Exit codes: `0` all cases `ok`; `1` at least one case or station failed numerically
(the document is written); `2` invalid input document, option, database path or
reactant; `3` accelerator or infrastructure error.

⚠ 2026-09-12: `apthermo` is the tool command name (`PackAsTool`, `ToolCommandName`);
the assembly is `AerospacePropellantThermodynamics.Cli`, named after its namespace as
the root requires, so a direct run is `dotnet AerospacePropellantThermodynamics.Cli.dll …`.
The sketch's `species` and `devices` had no output form; they write JSON (CSV for
`species`) like the solving commands. `--database` and `--threshold` apply to `states`
too, and `--output` and `--format` to the listings.

## Entry point ✅

```csharp
namespace AerospacePropellantThermodynamics.Cli;

public enum ExitCode { Ok = 0, CaseFailed = 1, InvalidInput = 2, Infrastructure = 3 }

public static class Program
{
    public const string ToolName = "apthermo";
    public static string Version { get; }                                   // the assembly's informational version
    public static int Main(string[] args);
    public static int Run(string[] args, TextWriter output, TextWriter error);   // in-process: documents to output or the --output file, messages to error
}
```

## Input document ✅

A rocket problem for a propellant given by database reactants and an
oxidizer-to-fuel ratio, swept over the ratio and the chamber pressure:

```json
{
  "propellant": {
    "reactants": [
      { "name": "O2(L)", "role": "oxidizer", "amount": 1.0, "temperature": 90.17 },
      { "name": "H2(L)", "role": "fuel", "amount": 1.0, "temperature": 20.27 }
    ],
    "mixture": { "oxidizerToFuel": 6.0 }
  },
  "problem": {
    "type": "rocket",
    "chamberPressure": 7.0e6,
    "flow": "shifting-equilibrium",
    "areaRatios": [20.0, 77.5],
    "pressureRatios": [],
    "transport": true
  },
  "sweep": {
    "oxidizerToFuel": { "from": 4.0, "to": 8.0, "step": 0.25 },
    "chamberPressure": [5.0e6, 7.0e6, 1.0e7]
  },
  "engine": { "accelerator": "auto" }
}
```

A propellant by total mass fractions, with a custom reactant given by its formula and
enthalpy (the aluminized composite of the fixtures):

```json
{
  "propellant": {
    "reactants": [
      { "name": "NH4CLO4(I)", "role": "named", "amount": 0.68 },
      { "name": "HTPB", "role": "named", "amount": 0.14, "temperature": 298.15,
        "formula": { "C": 7.3165, "H": 10.3416, "O": 0.0674 }, "enthalpy": -1046.0 },
      { "name": "AL(cr)", "role": "named", "amount": 0.18 }
    ],
    "omit": ["AL(L)"]
  },
  "problem": { "type": "rocket", "chamberPressure": 7.0e6, "areaRatios": [8.0, 12.0], "flow": "frozen-at-throat" }
}
```

An equilibrium state of a mixture given by its element moles and enthalpy:

```json
{
  "propellant": {
    "elementMoles": { "C": 9.505849129331365, "H": 35.214695099119155, "O": 15.704786718374072, "N": 6.007718569653603, "Cl": 3.3293409533388547, "Al": 14.709996403305084 },
    "enthalpy": -1527829.408385985
  },
  "problem": { "type": "equilibrium", "kind": "hp", "pressure": 6.5e6, "transport": true }
}
```

(the same mixture as the state record above, given as a problem document; the
element moles must weigh one kilogram, as for a record).

An assigned-temperature sweep over pressure and temperature:

```json
{
  "propellant": {
    "reactants": [
      { "name": "O2(L)", "role": "oxidizer", "amount": 1.0 },
      { "name": "H2(L)", "role": "fuel", "amount": 1.0 }
    ],
    "mixture": { "oxidizerToFuel": 6.0 }
  },
  "problem": { "type": "equilibrium", "kind": "tp", "pressure": 1.0e6, "temperature": 3000.0 },
  "sweep": { "pressure": [1.0e5, 1.0e6, 1.0e7], "temperature": { "from": 2000.0, "to": 3000.0, "step": 500.0 } }
}
```

Fields:

- `propellant.reactants[]`: `name` (a database record, or the name of a custom
  reactant), `role` (`oxidizer`, `fuel`, `named`), `amount` (a mass fraction within
  the role group, or moles with `"amountKind": "moles"`; `mass-fraction` is the
  default), `temperature` (K; optional: 298.15 K for a record with polynomial
  intervals, the record's own temperature otherwise); a custom reactant adds
  `formula` (atoms per formula unit), `enthalpy` (J/mol at its temperature, required
  with `temperature`) and optionally `molarMass` (kg/kmol; derived from the formula
  when absent).
- `propellant.mixture`: `{ "oxidizerToFuel": r }` splits the kilogram between the
  oxidizer and fuel groups; without `mixture` the amounts are total mass fractions
  and no reactant may be an oxidizer next to a fuel (name them all `named`).
- `propellant.elementMoles` (mol/kg) with optional `enthalpy` (J/kg): the elemental
  form, instead of `reactants` and `mixture`.
- `propellant.omit`, `propellant.only`: product species never to consider, or exactly
  the species to consider; `only` may not be empty.
- `problem.type`: `rocket` with `chamberPressure` (Pa), optional `flow`
  (`shifting-equilibrium`, the default, `frozen-at-chamber`, `frozen-at-throat`),
  `areaRatios`, `pressureRatios` (p_c/p_e; reported before the area-ratio exits),
  `transport` (default false), `temperatureEstimate` (K, default the library's); or
  `equilibrium` with `kind` (`tp`, `hp`, `sp`), `pressure` (Pa), `temperature` (K,
  required for tp, an estimate for hp and sp), `enthalpy` (J/kg, hp only; the
  mixture's own when absent), `entropy` (J/(kg·K), sp only), `transport`.
- `sweep`: each entry a non-empty list or a range `{from, to, step}` that ends on a
  step; `oxidizerToFuel` (a propellant with `mixture.oxidizerToFuel` only) and
  `chamberPressure` for a rocket problem, `oxidizerToFuel`, `pressure` and, for tp,
  `temperature` for an equilibrium problem. The product of the lists is one batch,
  ratio-major, then pressure, then temperature.
- `engine.accelerator`: `auto`, `cpu` or `cuda`; the command line's `--accelerator`
  wins.

Units are SI throughout: Pa, K, J/kg, J/(kg·K), mol/kg; a custom reactant's enthalpy
in J/mol. Unknown fields, missing required fields and values of the wrong type are
errors naming the JSON path.

⚠ 2026-09-12: the sketch had `"omit": [], "only": []` on every propellant and a
custom reactant with `"role": "fuel"` inside an oxidizer-to-fuel propellant. An empty
`only` cannot mean "no species", so it is rejected; the lists are optional. A mixture
without `mixture` is the mass-fraction form, and the reactant roles must agree with
it. `amountKind` values are spelled `mass-fraction` and `moles`, like the flow names.

## Output document ✅

```json
{
  "run": {
    "tool": "apthermo", "version": "1.0.0", "command": "rocket", "inputs": ["problem.json"],
    "database": { "thermoPath": "data/thermo.inp", "transPath": "data/trans.inp", "thermoSha256": "…", "transSha256": "…" },
    "accelerator": { "kind": "cuda", "deviceName": "NVIDIA GeForce RTX 5070 Ti", "ilgpuVersion": "1.5.3", "libNvvmPath": "…", "libDevicePath": "…", "threadsOrMultiprocessors": 70 },
    "timings": { "database": 0.31, "solve": 1.2 },
    "threshold": 5e-6
  },
  "cases": [
    {
      "index": 0,
      "inputs": { "oxidizerToFuel": 6.0, "chamberPressure": 7.0e6 },
      "status": "ok",
      "mixture": { "elementMoles": { "H": 141.73, "O": 53.57 }, "enthalpy": -986308.28 },
      "stations": [
        {
          "name": "chamber", "status": "ok",
          "temperature": 3485.02, "pressure": 7.0e6, "density": 2.9, "enthalpy": -986308.28, "internalEnergy": -3400000.0, "entropy": 17000.0, "gibbsEnergy": -60000000.0,
          "molarMass": 13.4, "mixtureMolarMass": 13.4, "cpFrozen": 4300.0, "cpEquilibrium": 8400.0, "cvFrozen": 3700.0, "cvEquilibrium": 7000.0,
          "dlnVdlnT": 1.35, "dlnVdlnP": -1.03, "gammaS": 1.14, "soundSpeed": 1570.0, "velocity": 0.0, "mach": 0.0,
          "performance": { "areaRatio": 0.0, "pressureRatio": 1.0, "characteristicVelocity": 2330.0, "thrustCoefficient": 0.0, "specificImpulse": 0.0, "vacuumSpecificImpulse": 0.0,
                           "specificImpulseSeconds": 0.0, "vacuumSpecificImpulseSeconds": 0.0 },
          "moleFractions": { "H2O": 0.65, "H2": 0.25, "OH": 0.04 },
          "condensedMassFractions": {},
          "transport": { "status": "ok", "viscosity": 1.0e-4, "frozenConductivity": 0.7, "reactingConductivity": 3.0, "frozenPrandtl": 0.6, "reactingPrandtl": 0.5,
                         "frozenHeatCapacity": 4300.0, "equilibriumHeatCapacity": 8400.0, "estimatedMoleFraction": 0.0,
                         "speciesCount": 9, "reactionCount": 7, "estimatedSpeciesCount": 0, "traceEliminations": 0, "capped": 0 }
        }
      ]
    }
  ]
}
```

Every case carries `index` (its position), `inputs` (the values that vary in the
batch: the ratio and the chamber pressure of a rocket case; the ratio, `kind`,
`pressure` and the assigned target of an equilibrium case; the record itself for
`states`), `status`, `mixture` (the element moles in mol/kg and the enthalpy in J/kg
the case started from, `null` when none was given), and `stations`: chamber, throat,
`exit1`… for a rocket case, one station named `state` for an equilibrium case. A
station carries every field of the library's `MixtureState` under its camel-case
name, `performance` with every field of `PerformanceFigures` plus the two
`…Seconds` conversions (rocket stations only), the compositions by name above the
threshold (`moleFractions` over all species, `condensedMassFractions` as n_j M_j),
and `transport` with `status` and, when it is `ok`, every field of
`TransportFigures` (present only when transport was requested). A non-finite number
is written as `null`. Statuses are the library's `CaseStatus` names in camel case.

The CSV form has one row per case and station: `case`, the scalar inputs, `station`,
`status`, the state fields, the performance fields with the two conversions,
`transportStatus` and the transport fields, in that order; cells that do not apply
are empty; compositions are not in CSV. Numbers are written in round-trip form.

⚠ 2026-09-12: the sketch had `elementMoles` and `reactantEnthalpy` on the case, a
`performance` list per exit next to the stations, only a few named state fields, and
no `index`, `inputs` or `mixture`. The library reports the figures per station and
the mixture as one record, so the document does the same; the field lists are the
library's structs, enumerated by reflection, so that a field added there reaches the
document without a second list. The `run.timings` are the tool's phases (`database`
load, `solve`), not the engine's, which the front door does not expose; `run` also
names the command, the input files and the threshold.

## Errors

| Situation | Behaviour |
|---|---|
| no command, an unknown command or option, a wrong argument count, an option that does not apply, a bad option value | message on standard error, exit code 2, no document |
| malformed JSON, unknown field, missing required field, wrong type, an empty `only`, a range that does not end on a step | message with the JSON path on standard error, exit code 2, no document |
| a document whose problem type does not match the command | message naming the right command, exit code 2 |
| unknown reactant, temperature out of range, an element without a record, a rocket case without enthalpy, transport without `trans.inp` | the library's message, exit code 2 |
| a state record or a `propellant.elementMoles` whose composition does not weigh one kilogram with the database's atomic weights within the front door's tolerance (a doubled record, mol/g, kmol/kg) | the record's source and the library's reason, `records.json: record 0: the composition weighs 2000.03 g with the database's atomic weights; element moles are per kilogram of mixture, so it must weigh 1000 g within 1 %` (`records.jsonl:2:` for JSON Lines, `problem.json: $.propellant.elementMoles:` for a document), exit code 2, no document |
| input file or database directory not found | message with the path, exit code 2 |
| accelerator unavailable, ILGPU mismatch, an unexpected failure | the message, exit code 3; for an accelerator, every path tried |
| a case or station failed numerically | the document is written with the status per case and station; exit code 1 |

## Side effects

Reads the input documents and the database files; writes the output document to the
given path (UTF-8, no byte-order mark) or to standard output; `devices` creates a CPU
engine and tries to create a CUDA engine. No other file, no network.

## Out of scope

- A CEA-like formatted text table.
- Plotting, interpolation, optimization over the sweep: consumers of the document.
