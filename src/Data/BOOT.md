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
- Loading the full `thermo.inp` (1.2 MB, about 2 100 records) takes under one second
  on the reference machine.

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
  each with its own temperature range; the node does not relate them.
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

## Taboos

- No evaluation of the polynomials here: that is `Thermo`, and one formula lives once.
- No unit conversion: the model carries the file's units (J/mol, kg/kmol, μP,
  μW/(cm·K)), and the conversion is the consumer's contract.
- No species selection or filtering rules: they are propellant knowledge (`Problems`).
- No thermodynamic constant in this node, not even R.
- No reference to ILGPU: this node must stay usable by tooling without an accelerator.
