# BOOT.md — Problems

## Purpose

The front door of the library: propellants and reactants, the assembly of the
chemical system (elements, candidate species, element moles and enthalpy per kilogram
of propellant), the problem and result types a user works with, and the
orchestration of `Data`, `Thermo`, `Transport` and `Execution` into one call. The
conventions here (what a reactant is, how an oxidizer-to-fuel ratio becomes mass
fractions, which species are candidates) are knowledge about propellants and about
NASA CEA's habits, not about solving, which is why they are a node of their own.

## Invariants

- **The species set of a batch is fixed by the element set**, not by the amounts:
  the candidate list depends only on the elements present in the union of the
  reactants, or in the union of the elemental records of a state batch; changing
  amounts, pressures or exits never changes the table. Cases of one batch share one
  `SpeciesTable`; a case in which an element is absent runs with the species
  containing it inactive (see the `Equilibrium` contract), not with another table.
- **Two front doors, one path.** A mixture given by reactants and a mixture given by
  element moles and enthalpy meet in the same `ElementalMixture` before anything else
  happens; every solve starts from element moles per kilogram and an enthalpy, whatever
  the input was.
- **Candidate species are chosen by one rule**: every gaseous product species of the
  database whose elements are all among the propellant's elements, plus every
  condensed product species under the same condition, minus the `Omit` list, or
  exactly the `Only` list when given; ionized species (names ending in `+` or `-`, and
  `e-`) and inert pseudo-element records are never candidates in version 1.
- **Element moles and enthalpy are computed from the database records**, per
  kilogram of propellant: `b_i = Σ_k w_k a_ik / M_k`, `h_0 = Σ_k w_k H_k(T_k) / M_k`,
  with `H_k(T_k)` from the reactant's own polynomial at its temperature, or the
  assigned enthalpy for records without intervals; a reactant temperature outside the
  record's range is an error, not an extrapolation.
- **Amounts are mass based.** Oxidizer and fuel amounts within their group are
  normalized to one; the oxidizer-to-fuel ratio splits the kilogram as
  `w_ox = OF / (1 + OF)`, `w_fuel = 1 / (1 + OF)`; a propellant given by total mass
  fractions is used as given after normalization to one; mole amounts are converted
  to mass with the record's molar mass before anything else.
- **SI in, SI out, names out.** Public types carry SI units and species names; no
  index leaves this node.
- **Statuses become results or exceptions, once.** A per-case failure is a
  `CaseStatus` in the result record; an infrastructure failure is an exception from
  `Execution` passed through; nothing is retried silently.
- **Immutable inputs.** Propellants and problems are immutable records; a solve never
  mutates them.

## Dependencies

- [Data](../Data/API.md) — the species database and atomic weights.
- [Thermo](../Thermo/API.md) — table building, `MixtureState`, `CaseStatus`, the gas constant.
- [Performance](../Performance/API.md) — `FlowModel`, `ExitSpecification`, `PerformanceFigures`.
- [Transport](../Transport/API.md) — transport table building and `TransportFigures`.
- [Execution](../Execution/API.md) — the engine and the batch containers.

Outside the tree: the .NET base class library.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Ordinary .NET code; the only allocations of a solve happen here and in `Execution`.
- Reactants are database records by name, or custom reactants given by name, formula
  (element counts), molar mass (derived from the formula and the atomic weights when
  not given), enthalpy at a temperature (J/mol), and that temperature. This is how
  binders such as HTPB are defined, exactly as CEA's exploded-formula reactants.
- Mixture specifications supported in version 1: oxidizer-to-fuel ratio, total mass
  fractions, per-reactant moles (converted to mass). Equivalence ratios and percent
  fuel are not in version 1 (they need element valences typed into code, which the
  root forbids; a later version may read them from a data file).
- The database is loaded by the caller and passed in; this node never opens files.
- Element order of a chemical system: the order of first appearance in the reactants
  (oxidizers, then fuels, each in the order given); species order: gaseous species in
  database order, then condensed species in database order. Both are reported in the
  result, because compositions are returned by name.
- Results carry the composition of every station as mole fractions of the gaseous
  phase and mass fractions of condensed species, both by name, without a threshold;
  thresholds are a presentation concern of the command line.
- Batch construction: one propellant definition (reactant set and temperatures) with
  per-case amounts (oxidizer-to-fuel ratio or mass fractions), chamber pressure and
  exit values; the number of exits per batch is fixed by the batch, the values vary
  per case. A state batch is a list of records, each with its own element moles,
  pressure and one target (enthalpy, temperature or entropy); its element set is the
  union over the records.
- Units at this boundary: element abundances are accepted in mol per kg and passed
  to the numerical nodes in kmol per kg (the CEA convention), the one conversion this
  node makes besides mass normalization; enthalpy in J/kg is passed unchanged.
- Element symbols are matched to the database spelling case-insensitively (`Al`,
  `al` and `AL` are the same element); the result reports the database spelling.
- Single-case calls are batches of one.

## Acceptance criteria

- [ ] For the RP-1311 examples and the four reference propellants, the element moles
      per kilogram and the reactant enthalpy per kilogram computed here equal the
      reference's (the fixtures record `b_i` and `h_0`, or the values recomputed from
      the reference's weights and `calc_property`) within 1e-10 relative.
- [ ] The candidate species list for each fixture case equals the reference's product
      list under the same `Omit` list (the reference lists its products; compared as sets).
- [ ] A custom reactant (the AP/binder case's binder) produces the reference `b_i` and `h_0`.
- [ ] An `ElementalMixture` built from the `b_i` and `h_0` of a fixture propellant
      gives the same rocket and equilibrium results as the propellant itself (bit for
      bit on the same accelerator); a state batch of the fixture stations reproduces
      the fixtures within the tolerance table, including records where an element of
      the batch is absent.
- [ ] End-to-end: the four reference propellants' rocket results through this node
      match the fixtures within the tolerance table (the same tests as the front door
      tests node's L2; listed here because this node's orchestration is what they exercise).
- [ ] A reactant temperature outside its record's range, an unknown reactant, a
      mixture with a zero-mass group, or an element without an atomic weight are
      rejected with the reactant's name in the exception, before any kernel runs.
- [ ] Two identical batches produce identical results (statuses and numbers).

## Taboos

- No numerical formula of the solvers here: this node computes only what a
  propellant definition implies (`b_i`, `h_0`, tables).
- No file access: the database comes from the caller.
- No silent defaults for missing data: an unknown species or a missing enthalpy is an error.
- No unit other than SI in a public type; no seconds for specific impulse.
- No index-based composition in a result: names only.
