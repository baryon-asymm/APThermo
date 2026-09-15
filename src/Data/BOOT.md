# BOOT.md — Data

## Purpose

Reads the NASA thermodynamic and transport databases (`thermo.inp`, `trans.inp`, the
files NASA CEA ships) into an immutable object model. It is the only node that knows
the file formats. It is ordinary .NET code with allocations and strings, which is why
it is separate from the kernel-capable `Thermo` node that consumes its output.

## Invariants

- **Lossless numeric parsing.** Every numeric field is read by the fixed column layout
  of the NASA format, `D` and `E` exponent letters are both accepted, and the double
  produced equals the one Fortran list-directed reading would produce. Checked by
  fixture records whose expected values are produced by an independent reader of the
  same files (`tests/Data.Tests/transcribe.py`: whitespace tokens and a number
  pattern instead of fixed columns), so that two different readings must agree.

  ⚠ 2026-09-12: this invariant said the fixtures were "transcribed from the file by
  hand and by round-trip formatting". No hand transcription was made: eleven records
  with up to three intervals of fourteen numbers each are safer transcribed by a second
  program than by eye, and no round-trip formatting exists. Found when the fixtures
  were written.
- **A record is whole or absent.** A species or transport record is either parsed in
  full or the load fails with the file name and the line number; no partial records
  reach the model.
- **Immutable after load.** The database, its lists and its records never change;
  lookups are by exact, case-sensitive name; the file order is preserved (products in
  file order, then reactants in file order), and the section of every record is kept.
- **Nothing is normalized.** Species names and element symbols are stored exactly as
  in the file (`AL2O3(a)`, `NH4CLO4(I)`, element symbols `AL`, `CL`), only trailing
  blanks trimmed. Names are the key by which every other node addresses species.
- **Numbers come from the record.** Molar mass and formation enthalpy are the fields
  of the record, never recomputed. Atomic weights are read as the molar mass of the
  monatomic gaseous species with the element's symbol (`AL`, `CL`, `H`, `O`, …), the
  symbol compared case-insensitively so that `Al` and `AL` name the same record; an
  element without such a species has no atomic weight and the request fails.
- **Reads only.** No network, no writes, no environment variables.

## Dependencies

None.

Outside the tree: the .NET base class library only. The data files themselves are
`data/thermo.inp` and `data/trans.inp`, committed verbatim from github.com/nasa/cea
(Apache-2.0) with the upstream commit hash recorded in `data/NOTICE`.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Ordinary .NET code, not kernel-compatible; it never runs on an accelerator.
- The files are read from paths given by the caller; the node has no default path.
- Encoding: the files are 7-bit ASCII; they are read as Latin-1 so that a stray byte
  never breaks a load.
- Loading the full `thermo.inp` (1.2 MB, about 2 100 records) took under one second
  on the reference machine (measured 2026-09-12; a figure, not a budget: since
  2026-09-14 no test holds it, because a wall-clock bound in the fast set reddens on
  a busy machine for no defect, the test review's F-TK-14).

  ⚠ 2026-09-12: stood "about 3 800 records", a figure from memory. The independent
  scan of the committed file (`ThermoLoadTests.Every_record_of_the_file_is_parsed`)
  counts 2 030 product and 81 reactant records.

### Format facts of `thermo.inp` (NASA Glenn, McBride, Zehe and Gordon 2002)

- Lines starting with `!` before the `thermo` line are comments. The `thermo` line is
  followed by one line with the default interval boundaries (200, 1000, 6000, 20000 K)
  and a date; the node stores them as provenance and does not use them otherwise.
- Product species follow until the line `END PRODUCTS`; reactant-only records follow
  until `END REACTANTS`.
- Record line 1: columns 1–18 name (no name in the committed file is longer than 15
  characters, and columns 16–18 are blank in every record), columns 19–80 a comment
  (source, reference).

  ⚠ 2026-09-12: stood "columns 1–24 name, columns 25–80 comment", a layout quoted
  from memory of the format description rather than checked against the file. In the
  committed file every comment starts in column 19 (`H2O               Hf:Cox,1989.
  …`), so the name field ends at column 18. Found when the fixture comparison of the
  comment field was written.
- Record line 2: columns 1–2 number of temperature intervals `N`; columns 4–9 the
  date code; columns 11–50 five pairs of (element symbol, 2 characters; count,
  `F6.2`), zero pairs dropped; columns 51–52 phase (`0` gas, anything else
  condensed); columns 53–65 molar mass, kg/kmol; columns 66–80 formation enthalpy at
  298.15 K in J/mol, or, when `N = 0`, the assigned enthalpy in J/mol.
- When `N = 0` (reactant-only records such as `O2(L)`, `H2(L)`, `RP-1`, `N2O4(L)`): one
  more line whose first field (columns 1–11) is the temperature in K at which the
  assigned enthalpy holds; the rest of the line is zeros.
- For every interval: one line with `TLow` (columns 1–11), `THigh` (columns 12–22),
  the number of coefficients (`7`, columns 23–23), eight exponents of T (`F5.1`, eight
  fields, the eighth unused), and `H(298.15) − H(0)` in J/mol (columns 66–80); then
  two lines of coefficients in `D16.9`: `a1 … a5` on the first, `a6 a7` and, after a
  blank field, `b1 b2` on the second.
- Meaning of the coefficients (with the usual exponents −2, −1, 0, 1, 2, 3, 4):
  `Cp°/R = a1 T⁻² + a2 T⁻¹ + a3 + a4 T + a5 T² + a6 T³ + a7 T⁴`,
  `H°/RT = −a1 T⁻² + a2 ln T / T + a3 + a4 T/2 + a5 T²/3 + a6 T³/4 + a7 T⁴/5 + b1/T`,
  `S°/R = −a1 T⁻²/2 − a2 T⁻¹ + a3 ln T + a4 T + a5 T²/2 + a6 T³/3 + a7 T⁴/4 + b2`.
  The node stores the exponents and coefficients; it does not evaluate them.
- Interval bounds are stored as written. Eleven condensed records of the committed file
  carry a first interval whose upper bound is not above its lower one (`Br2(cr)`
  300..265.9, `Si(cr)` 300..298.15, `U3O8(II)` 300..300: phases with data at 298.15 K
  only); the node neither rejects nor reorders them, and the tests node keeps them on
  its approved anomaly list.
- Condensed phases of one substance are separate records (`AL2O3(a)`, `AL2O3(L)`),
  each with its own temperature range; the node does not relate them. One condensed
  substance may also be written as several records under one name, one per
  temperature range (`Cr(cr)`, `Fe(a)`, `Cr2O3(I)` with three, and seven more of the
  committed file): the indexer returns the first, `Records` returns them all in file
  order, and joining them is the consumer's rule (`Thermo`). Recorded 2026-09-14:
  until then the fact was stated only in the consumer's document, and the consumer
  rebuilt the index the file implies (the clean-code review's F-TD-06).
- CEA's "inert" records (`InertO2`, `InertH2(L)`, `InertAir`, …) are the records whose
  name starts with `Inert`; their formulas use the pseudo-element symbols `IC`, `IH`,
  `IN`, `IO`. They are parsed like any other record and flagged by the name prefix.

  ⚠ 2026-09-12: stood "flagged by the presence of an element symbol starting with
  `I`". A symbol cannot be the flag: `I` is iodine and `IN` is indium (`In`, `InCL3`,
  …), both ordinary elements of the file. Found by a scan of the formula symbols made
  while implementing the flag.

### Format facts of `trans.inp`

- First line: a title. Then blocks: a header line with one species name (columns
  1–16) or two names (columns 1–16 and 17–32) for a binary interaction, the code
  `V<n>C<m>` (number of viscosity and conductivity fits), and a reference; then `n`
  lines starting with `V` and `m` lines starting with `C`, each with `TLow`, `THigh`
  and four coefficients `A B C D` in `E15.8`-like fields, some written without the
  `E` (`0.61205763E 00` means `0.61205763E+00`).
- Meaning: `ln η = A ln T + B/T + C/T² + D` with η in micropoise; the same form for
  the conductivity in μW/(cm·K). The node stores the fits and the units as in the
  file; conversion to SI belongs to `Transport`.

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). Each
parser is the record structure of its file, one class per file in this directory and
namespace, everything internal but the database types; the parsed model is
unchanged, and the fixture and corruption tests of the tests node are the proof.

| Type | Responsibility | Visibility |
|---|---|---|
| `SpeciesDatabase` | the contract; gains `Records(string name)`, the records of a name in file order, built once at load beside the index; the atomic weights are built at load too, so the type is immutable and shareable once loaded | public, contract grown by `Records` |
| `ThermoFile` | the skeleton of `thermo.inp`: comments, the `thermo` line, the header, the two sections, the record loop | internal |
| `SpeciesRecordReader` | one record, whole or absent: the identity line, the properties line (`N`, the date code, the formula pairs, the phase, the molar mass, the formation enthalpy), the assigned-temperature line | internal |
| `IntervalReader` | one interval: the bounds and exponents line, the two coefficient lines | internal |
| `RecordColumns` | the column map of the format facts above as named constants, so that the code reads against that table field by field | internal |
| `FixedColumns` | a field by its columns, for both files | internal |
| `LineErrors` | a field error stamped with its line and file, for both parsers, with an `Action` form so that no reader returns a value nobody reads | internal |
| `TransParser` | the block loop of `trans.inp` | internal |
| `TransportBlockReader` | one block: the header (the names, the `VnCm` code, the reference) and the fit lines with the V/C dispatch and the count check | internal |

Decisions taken with the review of 2026-09-14:

- **Several records under one name are a format fact of this node** (the bullet
  under the format facts): the indexer returns the first, `Records` returns them all
  in file order, and `Thermo` no longer rebuilds that index (the review's F-TD-06). A
  contract change, recorded in `API.md` with its ⚠; the snapshot moves in the same
  commit.
- **One sentinel convention.** The section and end markers are compared ordinally in
  the file's own case (`END PRODUCTS`, `END REACTANTS`, `end`); the `thermo` line
  alone is matched case-insensitively, as the format description allows (F-TD-12).
- **A negative interval count is a format error** stamped with its line, like every
  other bad field, instead of an `ArgumentOutOfRangeException` without file or line
  (F-TD-08); the seventh corruption case of the tests node.
- **The record constructors are the declared exception** to the parameter rule:
  `Species` (11 parameters) and `TemperatureInterval` (7) mirror the file's fields
  one to one; their single construction sites use named arguments, so a swap cannot
  compile unnoticed (F-TD-07).
- **The load time is a measurement, not a criterion** (Constraints); the wall-clock
  test of the tests node goes (F-TK-14).
- **`SpeciesRecordReader` does not translate its own field errors.** A first pass left
  the `FieldException → DatabaseFormatException` wrap (BOOT.md, `LineErrors`) inside
  `SpeciesRecordReader.Read`, which put it at Ce 12 (`DatabaseFormatException`,
  `FieldException`, `ElementCount`, `FixedColumns`, `FortranNumber`, `IntervalReader`,
  `LineErrors`, `RecordColumns`, `Species`, `SpeciesPhase`, `SpeciesSection`,
  `TemperatureInterval`), two over the root's limit. `ThermoFile.Parse` already holds
  the record's start line (`first = i`, before calling `Read`) for its own errors, so
  the wrap moved to its call site instead: `SpeciesRecordReader` now lets
  `FieldException` propagate, and `ThermoFile` catches it there. `Read`'s Ce drops to
  10 (at the limit); `ThermoFile`'s rises to 5. No behaviour changed — the corruption
  tests assert the same file, line and message before and after.

  ⚠ 2026-09-15: this decision is reversed. Its "Ce 12" and the "10 (at the limit)" it
  produced were both textual counts against that day's Ce limit of 10; the dependency
  check's own walk, first used to measure Ce by the protocol tests node's
  `CouplingMeasures` on 2026-09-14, gives `SpeciesRecordReader` 9 with the wrap moved
  out (the figure this node's own `## Shape exceptions` section recorded once the walk
  measured Ce) and 11 with the wrap back inside — both under the root's limit of 14,
  the same day recalibrated to the walk instead of the text (root `BOOT.md`). The
  premise for the move was gone before this node's own figures were next read against
  it. The wrap is back inside `SpeciesRecordReader.Read` (its `fcab80e` form, with
  `var first = i` held at entry), `ThermoFile.Parse` calling `Read` directly again:
  `Read` measures Ce 11 by the walk, `ThermoFile` 4. No behaviour changed — the
  corruption tests assert the same file, line and message before and after. Found by
  the clean-code repair review (R-Data-1).

  ⚠ 2026-09-15: the paragraph above first said the walk was "not written until the
  protocol tests node's `ShapeTests` phase" and that the `## Shape exceptions` figure
  came "once that walk existed". The walk is the dependency check's, written with that
  node's reflection checks on 2026-09-13 (`DependencyTests`); what came on 2026-09-14
  was its use for Ce (`CouplingMeasures`, which reads the same walk). Found at the end
  sweep of the clean-code pass, from the commits that added the two files.
- **Size.** No method over 60 lines, no control flow nested deeper than 3, no more
  than 6 parameters (the two constructors aside); no type names more than 14 distinct
  types of the tree (its efferent coupling, Ce) — the root's own limit, recalibrated
  the same day on the dependency check's walk, by which the protocol tests node's
  `ShapeTests` measures this node.

Decided 2026-09-15 (distribution phase, root `BOOT.md`, `## Delivery`, `Data`):

- **The bundled database is three `EmbeddedResource` items, not a fourth parser.**
  `data/thermo.inp`, `data/trans.inp` and `data/NOTICE` are linked into
  `APThermo.Data.csproj` from `data/` (never copied) under the manifest names
  `APThermo.Data.Bundled.thermo.inp`, `.trans.inp`, `.NOTICE`, chosen once and
  independent of the project's file layout so a later reshuffle of `## Structure`
  cannot silently rename them out from under a consumer.
  `SpeciesDatabase.LoadBundled()` reads the two database resources as raw bytes,
  decodes them Latin1 exactly as `Load` decodes files, hashes the same raw bytes, and
  calls the private `Build` both `Load` and `Parse` already call: the embedding adds a
  byte source, not a second reading of the format. `BundledNotice()` reads the third
  resource as UTF-8 text (the file is 7-bit ASCII, so the two encodings agree).
- **No new public type.** `LoadBundled` and `BundledNotice` are two more static
  members of `SpeciesDatabase`; no `EmbeddedResource` name is public, so the manifest
  names may still change without an API break as long as the two methods keep
  reading the same files.

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Species.Species` | parameters | 11 | mirrors the file's fields one to one (the decision "The record constructors are the declared exception to the parameter rule"); its single construction site names its arguments |
| `TemperatureInterval.TemperatureInterval` | parameters | 7 | mirrors the file's fields one to one, as `Species` above; its single construction site names its arguments |

⚠ 2026-09-15 (distribution phase): both constructors became `internal` (root `BOOT.md`,
Delivery: Tree contracts, M1; `SCRATCH/api-review-report.md`): no consumer built a
`Species` or a `TemperatureInterval`, only `SpeciesDatabase.Load`/`Parse` (through
`SpeciesRecordReader` and `IntervalReader`) ever did. The two rows above are
unchanged: the constructors still mirror the file's fields one to one, and their one
construction site still names every argument, now in `camelCase` matching the
constructors' own parameter names rather than the records' former positional
`PascalCase` ones. `DatabaseProvenance`, `TransportEntry` and `TransportFit` gained
internal constructors the same way; none is a declared parameter-count exception (4,
5 and 6 parameters respectively, within the rule).

No type of this node names more than 11 distinct types of the tree by the dependency
check's walk (`SpeciesRecordReader`, after R-Data-1 reversed the Ce-driven move of
2026-09-14), below the root's limit of 14: no efferent coupling row is needed.

## Acceptance criteria

- [x] 2026-09-12 — The number of product species and of reactant records parsed equals
      the counts produced by an independent line scan of the file in the test
      (machine-generated, not typed): `ThermoLoadTests.Every_record_of_the_file_is_parsed`.
- [x] 2026-09-12 — Fixture records (the species listed in `tests/Data.Tests/transcribe.py`,
      one file each under `tests/Data.Tests/records/species/`: `H2O`, `AL2O3(a)`,
      `AL(cr)`, `C(gr)`, `e-`, `O2(L)`, `H2(L)`, `RP-1`, `N2O4(L)`, `NH4CLO4(I)`,
      `C2H8N2(L),UDMH`) parse to the expected fields stored in the fixture files,
      including every coefficient and exponent:
      `ThermoLoadTests.Fixture_records_parse_to_the_transcribed_values`.
- [x] 2026-09-12 — Every record's intervals are ascending and contiguous (`THigh` of
      one equals `TLow` of the next) or the record is on the approved anomaly list
      (`tests/Data.Tests/records/interval-anomalies.approved.txt`), which the test
      regenerates and compares: `ThermoLoadTests.Interval_anomalies_equal_the_approved_list`.
      The approved list holds the condensed records with a non-ascending first interval;
      the file has no contiguity gap.
- [x] 2026-09-12 — Every numeric form of the two files (`D` exponents; `E` exponents
      with a sign or with a blank in place of the sign; a sign in place of the exponent
      letter; bare decimals; blank fields) parses to the expected double:
      `FortranNumberTests.Parses_every_form_of_the_files`; and the fixture records of
      the second criterion agree with the independent Python reading.

      ⚠ 2026-09-12: stood "the single `E`-exponent record in the current file and the
      `D` records parse to the same doubles as an independent Python parse". The
      committed `thermo.inp` has no coefficient written with an `E` exponent; the `E`
      forms are in `trans.inp`. Found by a search over the file when the criterion was
      ticked.
- [x] 2026-09-12 — Transport: the numbers of single-species and pair blocks equal an
      independent scan (`TransLoadTests.Every_block_of_the_file_is_parsed`); the `H2`
      block (three viscosity, three conductivity fits) and the `CO`/`CO2` pair match a
      fixture (`TransLoadTests.Fixture_blocks_parse_to_the_transcribed_values`).
- [x] 2026-09-12 — A truncated or corrupted record fails the load with the line
      number of the bad field in the message (mutation tests on an in-memory copy of
      one record): `CorruptionTests`, six tests.
- [x] 2026-09-12 — `AtomicWeight("AL")` equals the molar mass of the record `AL`;
      `AtomicWeight` of a symbol without a monatomic record throws:
      `ThermoLoadTests.Atomic_weights_come_from_the_monatomic_species`.
- [x] 2026-09-14 — The decomposition of `## Structure`: no type over 400 lines (the
      largest new file, `SpeciesDatabase.cs`, 152), no method over 60, no nesting
      deeper than 3, no more than 6 parameters except the two record constructors
      (measured by the coder's scan at the close of the decomposition, the longest
      method `ThermoFile.Parse` at 57 lines and the highest efferent coupling
      `SpeciesRecordReader` at 10; the protocol tests node's `ShapeTests`, root
      `BOOT.md`, re-measures it once it exists); the public
      surface grown by `SpeciesDatabase.Records` only, `PublicSurface.approved.txt`
      moved in the same commit; every fixture, count, anomaly and corruption test of
      the tests node green unchanged (`dotnet test tests/Data.Tests`, 39 tests, the
      one new `Loading_the_full_file_takes_under_a_second` removal aside).

      ⚠ 2026-09-14: the parenthetical above first read "measured by the protocol tests
      node's `ShapeTests`". That test did not exist when the criterion was ticked (the
      protocol tests node's Shape level is still planned): the figures came from the
      coder's scan, which the parenthetical now names.
- [x] 2026-09-14 — `Records(name)` returns the records of every same-name group in
      file order and the indexer the first of them (`Cr(cr)`, `Fe(a)`, `Cr2O3(I)`
      among the names the tests node's own scan of the committed file finds
      repeated): `ThermoLoadTests.Every_record_of_a_repeated_name_is_returned_in_file_order`.
      A negative interval count fails the load naming its line, the seventh
      corruption case: `CorruptionTests.A_negative_interval_count_names_its_line`.
      The atomic weights are built once at load from a single pass over `Products`,
      so a loaded database is immutable and needs no lock; the existing
      `ThermoLoadTests.Atomic_weights_come_from_the_monatomic_species` covers the
      values unchanged. Both new checks seen red once, reverted: `Records` made to
      return only the first record turned the repeated-name test red on every
      repeated name found; the negative-count check removed from
      `SpeciesRecordReader.ReadProperties` turned the corruption test red with the
      bare `ArgumentOutOfRangeException` the wording above describes.
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-15 — The bundled database: the SHA-256 of each of the three embedded
      resources (`thermo.inp`, `trans.inp`, `NOTICE`) equals the SHA-256 of the
      matching file under `data/`
      (`BundledDatabaseTests.Embedded_resource_bytes_equal_the_committed_files`);
      `LoadBundled()` produces a database equal, species by species and coefficient
      by coefficient (Products, Reactants, every transport entry and fit, the
      provenance hashes and header), to `Load(data/thermo.inp, data/trans.inp)`
      (`BundledDatabaseTests.LoadBundled_equals_Load_on_every_species_and_coefficient`);
      `BundledNotice()` equals the text of `data/NOTICE`
      (`BundledDatabaseTests.BundledNotice_equals_the_committed_file`). Each seen red
      once (AGENTS.md §13): the embedded `thermo.inp` truncated to its first 100
      lines turned the hash test and the equality test both red; reverted, nothing of
      the mutation committed.

## Taboos

- No evaluation of the polynomials here: that is `Thermo`, and one formula lives once.
- No unit conversion: the model carries the file's units (J/mol, kg/kmol, μP,
  μW/(cm·K)), and the conversion is the consumer's contract.
- No species selection or filtering rules: they are propellant knowledge (`Problems`).
- No thermodynamic constant in this node, not even R.
- No reference to ILGPU: this node must stay usable by tooling without an accelerator.
