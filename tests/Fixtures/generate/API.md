# API.md — generate

Python, run with the interpreter of `.venv` (see the parent's `API.md` for the
environment). The node exposes a command line and, for the scripts, one function each.
Everything not listed here is internal and may change.

## Command line ✅

```console
$ .venv/Scripts/python regenerate.py [--check] [--sample] [kind ...]
$ .venv/Scripts/python <family>.py [--check] [kind ...]    # constants, thermo_functions, transport_fits, rp1311, propellants
```

Without `--check`: changed and missing fixtures are written, stale files of the
produced kinds are removed, and the exit code is 0. With `--check`: nothing is
written, every difference is printed (`changed`, `missing`, `stale`) and the exit code
is 1 when there is any. Kinds given as arguments restrict what is produced and swept.

Two comparison modes (the parent's "The binding step runs on the fixtures' own
platform and compares with a tolerance", 2026-09-30):

- **Exact text**, plain `--check` (and the comparison that decides what a run without
  `--check` rewrites): a regenerated document equals the committed file byte for byte
  but for the `generatedOn` date. Meant for the reference machine, whose platform wrote
  the files.
- **Document, field by field, with a tolerance**, `--check --sample`
  (`document_comparison.py`): the same keys in the same order, the same list lengths,
  every string, boolean and null equal, every number within the relative tolerance of
  the `regeneration` row of the parent's `tolerances.json` (1e-9), the numbers a
  `throat` document's mass-flux search derives (every number of its throat station, the
  `characteristicVelocity` of each station and `outputs.packageRocketThroat`) within the
  `throat` row (5e-5), and of the provenance block the keys `generatedOn`,
  `thermoLibSha256` and `transLibSha256` left out, every other key equal. A difference
  is printed under its `changed` line as the dotted path of the field, both values and
  the tolerance it broke. Meant for a hosted runner of the platform that wrote the files,
  whose CPU may round the last bits of the C runtime's `exp`, `log` and `pow` otherwise.

`--sample` (`regenerate.py` only, the parent's "The outputs are bound to the
generator" and "The sample of the binding step covers every script"): restricts the
run to one case per (script, kind) pair the committed fixtures record, plus every case
of the `throat` kind, all read from the committed files' own `generator.script`
fields. The stale sweep is off under a sample, since a sample deliberately produces
only part of each kind. With `--check --sample`, the exit code is also 1 when a
(script, kind) pair the sample intended to cover compared nothing at all, or when the
sample itself is empty, printed as `sample coverage: ...` lines.

## Modules ✅

```python
# writer.py
class Writer:
    def __init__(self, check: bool = False, only: list[str] | None = None,
                 only_cases: set[tuple[str, str]] | None = None, tolerant: bool = False) -> None: ...
    def case(self, kind: str, name: str, inputs: dict, outputs: dict, script_path: str,
             method: str = "cea-package") -> str: ...           # "unchanged" | "changed" | "missing" | "written" | "skipped"
    def finish(self) -> int: ...                                 # prints the summary, sweeps stale files, returns the exit code
def dumps(document: dict) -> str: ...                            # the canonical JSON text of a document

# document_comparison.py — the comparison of `--check --sample`; reads tolerances.json
class DocumentComparison:
    def __init__(self) -> None: ...
    def differences(self, expected: dict, actual: dict) -> list[str]: ...   # committed against regenerated; empty when they agree
def main_of(generate) -> int: ...                                # the standalone entry point of a family script

# constants.py, thermo_functions.py, transport_fits.py, rp1311.py, propellants.py
def generate(writer: Writer) -> None: ...

# cea_cases.py — case builders over the package; all values returned in SI
FLOW_SHIFTING, FLOW_FROZEN_CHAMBER, FLOW_FROZEN_THROAT: str
class Custom:                                                    # a reactant that is not a thermo.inp record
    def __init__(self, name: str, formula: dict[str, float], enthalpy_cal_per_mol: float,
                 temperature: float = 298.15, note: str | None = None) -> None: ...
def describe_reactants(reactants: list, weights, temperatures=None) -> list[dict]: ...
def make_mixtures(reactants: list, products: list[str] | None = None, omit: list[str] | None = None): ...
def solve_equilibrium(reac, prod, weights, kind: str, value_si: float, pressure_pa: float,
                      transport: bool, trace: float | None = None) -> dict: ...
def solve_rocket(reac, prod, weights, temperatures, chamber_pressure_pa: float, flow: str, transport: bool,
                 area_ratios=None, pressure_ratios=None, subsonic_area_ratios=None): ...
def rocket_outputs(solution, transport: bool, flow: str = FLOW_SHIFTING) -> dict: ...
def rocket_inputs(descriptions, products, chamber_pressure_pa, reactant_enthalpy, flow, transport, ...) -> dict: ...
def equilibrium_inputs(descriptions, products, kind, value_si, pressure_pa, transport, ...) -> dict: ...
def derive_equilibrium_cases(writer, script_path, base_name, solution, reac, prod, weights,
                             descriptions, transport, indices, of_ratio=None, only=None) -> None: ...

# `only` on the three input builders: the explicit product list a case was given, written to the
# inputs as "only" and absent when the package selected the products itself (added 2026-09-12)

# common.py — paths, the generator's own reader of the NASA files, element moles, unit factors
ROOT, DATA, CASES, THERMO_INP, TRANS_INP: str
BAR_TO_PA, KJ_TO_J, MILLIPOISE_TO_PA_S, MW_PER_CM_K_TO_W_PER_M_K, MICROPOISE_TO_PA_S, MICROW_PER_CM_K_TO_W_PER_M_K: float
def read_thermo(path: str = THERMO_INP) -> dict[str, Record]: ...
def read_trans(path: str = TRANS_INP) -> list[TransportBlock]: ...
def atomic_weight(records: dict[str, Record], symbol: str) -> float: ...
def element_moles(records: dict[str, Record], reactants: list[dict]) -> dict[str, float]: ...   # kmol per kg
def sha256_of(path: str) -> str: ...
def sha256_of_normalized(path: str) -> str: ...                  # CRLF normalized to LF first (scripts, requirements.txt)
def generator_sha256() -> str: ...                                # the generatorSha256 rule below, over generate/
def safe_name(name: str) -> str: ...                             # the file-name form of a case name
```

## Errors

| Situation | Behaviour |
|---|---|
| a rocket or equilibrium case does not converge | `RuntimeError` naming the package's error code; the run stops, nothing is written for the case |
| a species named in a script is not in the data file | `KeyError` with the name |
| a value that is not a number would be written | `ValueError` from the JSON encoder (`NaN` is forbidden) |

## Side effects

Writes and removes files under `../cases/` only; reads the data files and the package.

## Out of scope

- Deciding tolerances or judging differences: the parent's table and the test nodes.
- Running anything of the tree.
