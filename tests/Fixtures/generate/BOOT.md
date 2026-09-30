# BOOT.md — generate

## Purpose

The Python scripts that produce the reference fixtures of the Fixtures node: the cases
of the case matrix solved with the `cea` package (rocket and equilibrium problems),
values evaluated independently of both the package and the tree (the thermodynamic
functions and the transport fits, read from the committed NASA files), and the
package's gas constant. A node of its own because it is code, run in its own Python
environment, while the parent holds data files and a .NET loader.

## Invariants

- **Deterministic output.** The same scripts, package and data files produce
  byte-identical fixtures: the writer compares before writing and never rewrites a
  document whose regenerated content differs only in the date, so `generatedOn` is the
  day the content last changed.
- **The independent scripts share nothing with what they verify.** `thermo_functions.py`
  and `transport_fits.py` read the NASA files with the generator's own reader
  (`common.py`) and spell the formulas out in their general form; they do not import the
  package's solvers and cannot import the tree.
- **One writer.** Only `writer.py` writes files, and every document it writes carries
  the provenance the parent requires.
- **Inputs suffice to reproduce a case without its script**: reactants with mass
  fractions and temperatures (custom reactants spelled out with formula, molar mass and
  enthalpy), element moles per kilogram, the product species list the package used,
  the problem values in SI, and for derived cases the station they come from.
- **No fixture value is typed into a script.** Values come from the package or from
  evaluating the files; what is typed is the case matrix (inputs), as the parent's
  Constraints define it.
- **A case that does not converge stops the run.** Nothing is written for it; a
  fixture never carries an unconverged result.

## Dependencies

None.

Outside the tree: Python 3.11+, `cea` 3.3.4 and `numpy` 2.5.3 (`requirements.txt`;
the environment lives in `.venv`, ignored by git); the tree's `data/thermo.inp` and
`data/trans.inp`, read as files.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and the root. In addition:

- Layout: one module per fixture family (`constants.py`, `thermo_functions.py`,
  `transport_fits.py`, `rp1311.py`, `propellants.py`, `plateaus.py`, `throat_scan.py`
  since 2026-09-27), the case builders over the
  package in `cea_cases.py`, shared helpers in `common.py`, the writer in `writer.py`,
  the document comparison of the sampled check in `document_comparison.py` (2026-09-30),
  the driver `regenerate.py`. Every family module exposes `generate(writer)` and runs
  standalone.
- Unit conversion happens only here, through the named factors of `common.py`: bar to
  Pa, kJ to J, millipoise to Pa·s, mW/(cm·K) to W/(m·K), micropoise to Pa·s,
  μW/(cm·K) to W/(m·K). The package's units were established by a probe against known
  magnitudes before the factors were written.
- The package takes assigned enthalpies of custom reactants in cal/mol; the fixture
  records them in J/mol (thermochemical calorie, 4.184 J).
- Frozen rocket cases are generated without transport: the package's frozen expansion
  with transport on fails for product sets of about a hundred species (recorded in the
  parent's case matrix).
- `throat_scan.py` (2026-09-27) builds the throat family by the method and the guard
  the parent's case matrix states. It reuses the reactant lists and compositions of
  `plateaus.py` and `rp1311.py` by import, never by copy. The scan solves only through
  `cea_cases.solve_equilibrium`, and the guard's rocket solves only through
  `cea_cases.solve_rocket`.
- `generatorSha256` (2026-09-27) is the SHA-256 of the concatenation, over every `*.py`
  file of this directory and `requirements.txt` in ordinal order of their names, of the
  file name, a LF, and the file's bytes with CRLF normalized to LF. The script hashes
  use the same normalization, so that a Windows checkout and a Linux one agree. The
  fixtures node's tests recompute both from the committed files.
- JSON form: indent 2, LF, UTF-8, `NaN` forbidden, floats in Python's shortest
  round-trip form, numpy values converted to Python numbers.

## Acceptance criteria

- [x] 2026-09-12 — `regenerate.py` produces the seven kinds of the case matrix and
      `regenerate.py --check` exits 0 immediately afterwards; a fixture altered by hand
      is reported as `changed` with exit 1, a deleted one as `missing`, an extra file as
      `stale` (run by hand on the reference machine; the run printed 261 fixtures).

      ⚠ 2026-09-27: "the seven kinds" is now eight — `throat_scan.py` added the `throat`
      kind (the parent's case matrix). `regenerate.py --check` exits 0 on the current
      tree, 327 fixtures, none stale or missing.

      ⚠ 2026-09-27, the sodium case (the parent's case matrix): `propellants.py` gains
      `sodium_hp`, one hp file. `regenerate.py --check` exits 0 on the current tree, 328
      fixtures, none stale or missing.
- [x] 2026-09-12 — Every family script runs standalone and sweeps only the kinds it
      produces. Seen red once: `thermo_functions.py` run alone removed `constants/R.json`
      until `Writer.finish` was limited to the kinds of the run.

      ⚠ 2026-09-27: this criterion is true only kind by kind, not script by script, for
      a kind several scripts write (`tp`, `hp`, `sp`, `rocket`): the parent `BOOT.md`'s ⚠
      of this date records the hazard, found running `propellants.py` alone for the
      sodium case above.
- [x] 2026-09-12 — The derived equilibrium cases reproduce their station: the tp, hp
      and sp solves at a station's values return the station's temperature (for the
      LOX/LH2 throat, 3292.3746 K against 3292.3746 K), so the package's hp and sp inputs
      are the enthalpy and entropy in J/kg divided by R (probe before the scripts were
      written; the `derivedFrom.temperature` field lets a test node re-check every case).

- [x] 2026-09-30 — `regenerate.py --check --sample` compares as documents with a
      tolerance and plain `--check` stays exact text (the parent's last criterion, which
      holds the evidence: the mutations, each applied alone and seen to fail or pass as
      designed, and the full regeneration whose 336 files moved only in `generatorSha256`
      and `generatedOn`). The CI run on `windows-latest` is still owed there; this
      criterion states the local half only.

      ⚠ 2026-09-30: the Invariants above still say "byte-identical fixtures", which holds
      for plain `--check`; the sampled check on another machine of the platform accepts
      the differences the parent's rows allow, since the same scripts, package and data
      files give bit-identical output only on the machine that wrote the files.

## Taboos

- No hand-edited fixture, no fixture value typed into a script.
- No import of the tree's code and no call into it: the generator must not depend on
  what it verifies.
- No tolerance decided here: the parent's `tolerances.json` is the only table
  (`document_comparison.py` reads its `regeneration` and `throat` rows and holds no
  figure of its own).
- No network access at generation time: the package and the data are local.
