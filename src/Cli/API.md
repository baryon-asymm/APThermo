# API.md — Cli

Namespace `AerospacePropellantThermodynamics.Cli`, executable `apthermo`. The node
exposes a command line and two JSON document schemas. Everything not listed here is
internal and may change.

## Command line ⏳

```console
$ apthermo rocket problem.json [--output result.json] [--format json|csv] [--accelerator auto|cpu|cuda] [--database DIR] [--threshold 5e-6]
$ apthermo equilibrium problem.json [same options]
$ apthermo species [--find TEXT] [--database DIR]
$ apthermo devices
$ apthermo states states.json [--output results.json] [--format json|csv] [--transport] [--accelerator auto|cpu|cuda]
```

`states` takes a JSON array, a JSON Lines file, or several files, of state records
and solves them as one batch: this is the path for data produced by another
simulation. A state record is exactly this shape:

```json
{ "pressure": 1000000.0, "enthalpy": -1527829.408385985, "composition": { "C": 9.505849129331365, "H": 35.214695099119155 } }
```

with `pressure` in Pa, `composition` as element moles per kilogram of mixture (element
symbols in any case), and exactly one of `enthalpy` (J/kg, hp problem), `temperature`
(K, tp problem) or `entropy` (J/(kg·K), sp problem). Optional `areaRatios` or
`pressureRatios` on a record turn it into a rocket case with `pressure` as the chamber
pressure. Unknown fields are an error, so that a unit mistake cannot pass silently.

Exit codes: `0` all cases `Ok`; `1` at least one case failed numerically (document
written); `2` invalid input document; `3` accelerator or infrastructure error.

## Input document ⏳

```json
{
  "propellant": {
    "reactants": [
      { "name": "O2(L)", "role": "oxidizer", "amount": 1.0, "temperature": 90.17 },
      { "name": "H2(L)", "role": "fuel",     "amount": 1.0, "temperature": 20.27 },
      { "name": "HTPB",  "role": "fuel",     "amount": 0.14, "temperature": 298.15,
        "formula": { "C": 7.3165, "H": 10.3416, "O": 0.0674 }, "enthalpy": -1046.0 }
    ],
    "mixture": { "oxidizerToFuel": 6.0 },
    "omit": [], "only": []
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

A `propellant` may instead be given as an elemental mixture:
`"propellant": { "elementMoles": { "C": 9.5058, "H": 35.2147, "O": 31.2, "N": 4.1 }, "enthalpy": -1527829.4 }`,
element moles per kilogram and J/kg, and then `omit`/`only` apply as before.

Units: Pa, K, J/mol for a custom reactant's enthalpy at its temperature; amounts are
mass fractions within the role group unless `"amountKind": "moles"`. `flow` is one of
`shifting-equilibrium`, `frozen-at-chamber`, `frozen-at-throat`. `type` is `rocket`
or `equilibrium` (with `kind`: `tp`, `hp`, `sp`, and `pressure`, `temperature`,
`enthalpy`, `entropy` as needed). `sweep` is optional; each of its entries is a list
or a `{from, to, step}` range; the product of the lists is one batch.

## Output document ⏳

```json
{
  "run": {
    "tool": "apthermo", "version": "…", "database": { "thermoSha256": "…", "transSha256": "…" },
    "accelerator": { "kind": "cuda", "device": "NVIDIA GeForce RTX 5070 Ti", "ilgpu": "1.5.3" },
    "timings": { "warmUp": 0.0, "upload": 0.0, "kernel": 0.0, "download": 0.0 }
  },
  "cases": [
    {
      "inputs": { "oxidizerToFuel": 6.0, "chamberPressure": 7.0e6 },
      "status": "ok",
      "elementMoles": { "H": 0.0, "O": 0.0 },
      "reactantEnthalpy": 0.0,
      "stations": [
        {
          "name": "chamber",
          "temperature": 0.0, "pressure": 0.0, "density": 0.0, "enthalpy": 0.0, "entropy": 0.0,
          "molarMass": 0.0, "cpFrozen": 0.0, "cpEquilibrium": 0.0, "gammaS": 0.0, "soundSpeed": 0.0, "mach": 0.0,
          "moleFractions": { "H2O": 0.0 }, "condensedMassFractions": {},
          "transport": { "viscosity": 0.0, "frozenConductivity": 0.0, "reactingConductivity": 0.0, "frozenPrandtl": 0.0, "reactingPrandtl": 0.0 },
          "status": "ok"
        }
      ],
      "performance": [
        { "areaRatio": 20.0, "pressureRatio": 0.0, "characteristicVelocity": 0.0, "thrustCoefficient": 0.0,
          "specificImpulse": 0.0, "vacuumSpecificImpulse": 0.0, "specificImpulseSeconds": 0.0 }
      ]
    }
  ]
}
```

The CSV format has one row per case and station with the scalar fields above and
the performance figures of the station's exit; compositions are not in CSV.

## Errors

| Situation | Behaviour |
|---|---|
| malformed JSON, unknown field, missing required field, wrong unit name | message with the JSON path on standard error, exit code 2, no document |
| unknown reactant, temperature out of range | the library's message, exit code 2 |
| accelerator unavailable, ILGPU mismatch | the library's message, exit code 3 |
| a case failed numerically | the document is written with the status per case and station; exit code 1 |

## Side effects

Reads the input document and the database files; writes the output document to the
given path or to standard output.

## Out of scope

- A CEA-like formatted text table.
- Plotting, interpolation, optimization over the sweep: consumers of the document.
