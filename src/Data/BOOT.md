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
  fixture records whose expected values were transcribed from the file by hand and by
  round-trip formatting.
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
  monatomic gaseous species with the element's symbol (`AL`, `CL`, `H`, `O`, …); an
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
- Loading the full `thermo.inp` (about 1.2 MB, about 3 800 records) takes under one
  second on the reference machine.

### Format facts of `thermo.inp` (NASA Glenn, McBride, Zehe and Gordon 2002)

- Lines starting with `!` before the `thermo` line are comments. The `thermo` line is
  followed by one line with the default interval boundaries (200, 1000, 6000, 20000 K)
  and a date; the node stores them as provenance and does not use them otherwise.
- Product species follow until the line `END PRODUCTS`; reactant-only records follow
  until `END REACTANTS`.
- Record line 1: columns 1–24 name (columns 1–15 are the significant part in CEA;
  the whole 24 are kept), columns 25–80 a comment (source, reference).
- Record line 2: columns 1–2 number of temperature intervals `N`; columns 4–9 the
  date code; columns 11–50 five pairs of (element symbol, 2 characters; count,
  `F6.2`), zero pairs dropped; columns 51–52 phase (`0` gas, anything else
  condensed); columns 53–65 molar mass, kg/kmol; columns 66–80 formation enthalpy at
  298.15 K in J/mol, or, when `N = 0`, the assigned enthalpy in J/mol.
- When `N = 0` (reactant-only records such as `O2(L)`, `H2(L)`, `RP-1`, `N2O4(L)`): one
  more line whose first field (columns 1–11) is the temperature in K at which the
  assigned enthalpy holds; the rest of the line is zeros.
- For every interval: one line with `TLow` (columns 2–11), `THigh` (columns 12–22),
  the number of coefficients (`7`, columns 23–23), eight exponents of T (`F5.1`, eight
  fields, the eighth unused), and `H(298.15) − H(0)` in J/mol (columns 66–80); then
  two lines of coefficients in `D16.9`: `a1 … a5` on the first, `a6 a7` and, after a
  blank field, `b1 b2` on the second.
- Meaning of the coefficients (with the usual exponents −2, −1, 0, 1, 2, 3, 4):
  `Cp°/R = a1 T⁻² + a2 T⁻¹ + a3 + a4 T + a5 T² + a6 T³ + a7 T⁴`,
  `H°/RT = −a1 T⁻² + a2 ln T / T + a3 + a4 T/2 + a5 T²/3 + a6 T³/4 + a7 T⁴/5 + b1/T`,
  `S°/R = −a1 T⁻²/2 − a2 T⁻¹ + a3 ln T + a4 T + a5 T²/2 + a6 T³/3 + a7 T⁴/4 + b2`.
  The node stores the exponents and coefficients; it does not evaluate them.
- Condensed phases of one substance are separate records (`AL2O3(a)`, `AL2O3(L)`),
  each with its own temperature range; the node does not relate them.
- Records whose element symbols start with `I` followed by a symbol (`IO`, `IH`) are
  CEA's "inert" pseudo-elements (`InertO2(L)`); they are parsed like any other record
  and flagged by the presence of such a symbol.

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

- [ ] The number of product species and of reactant records parsed equals the counts
      produced by an independent line scan of the file in the test (machine-generated,
      not typed).
- [ ] Fixture records (`H2O`, `AL2O3(a)`, `AL(cr)`, `C(gr)`, `e-`, `O2(L)`, `H2(L)`,
      `RP-1`, `N2O4(L)`, `NH4CLO4(I)`) parse to the expected fields stored in a fixture
      file next to the test, including every coefficient and exponent.
- [ ] Every record's intervals are contiguous (`THigh` of one equals `TLow` of the
      next) or the record is on the approved anomaly list, which the test regenerates
      and compares.
- [ ] The single `E`-exponent record in the current file and the `D` records parse to
      the same doubles as an independent Python parse (fixture generated by a script
      kept in the tests node).
- [ ] Transport: the numbers of single-species and pair blocks equal an independent
      scan; the `H2` block (three viscosity, three conductivity fits) and the
      `CO`/`CO2` pair match a fixture.
- [ ] A truncated or corrupted record fails the load with the line number in the
      message (mutation tests on a copy of the file).
- [ ] `AtomicWeight("AL")` equals the molar mass of the record `AL`; `AtomicWeight` of
      a symbol without a monatomic record throws.

## Taboos

- No evaluation of the polynomials here: that is `Thermo`, and one formula lives once.
- No unit conversion: the model carries the file's units (J/mol, kg/kmol, μP,
  μW/(cm·K)), and the conversion is the consumer's contract.
- No species selection or filtering rules: they are propellant knowledge (`Problems`).
- No thermodynamic constant in this node, not even R.
- No reference to ILGPU: this node must stay usable by tooling without an accelerator.
