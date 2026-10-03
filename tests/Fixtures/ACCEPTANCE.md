# ACCEPTANCE.md — Fixtures

Readiness evidence of the Fixtures node (`AGENTS.md`, §6 and §15). The invariants and
constraints cited below as "above" or "below" are those of `BOOT.md`, which holds the
frame; this file holds the criteria that prove it, read only in this node. The case matrix
cited below is held in [generate/BOOT.md](generate/BOOT.md), which its scripts are bound by.

## Acceptance criteria

- [x] 2026-09-12 — Every fixture regenerates byte-identically from the committed
      scripts with the pinned package on the reference machine:
      `regenerate.py --check` exits 0 on the committed `cases/` directory after a full
      regeneration, and exits 1 with the file named when a fixture is altered, missing
      or stale (run by hand on the reference machine).
- [x] 2026-09-12 — The loader reads every fixture and the tolerance table, and a
      fixture missing a field of its kind fails the loader with the file name and the
      field: `Fixtures.Tests` (`FixtureLoadingTests`, `ToleranceTableTests`,
      `MalformedFixtureTests`).
- [x] 2026-10-02 — The HTPB definition is decided and cited, by the owner's decision of
      that day: the definition of J. C. Thomas and E. L. Petersen, "HTPB Heat of
      Formation: Literature Survey, Group Additive Estimations, and Theoretical Effects",
      AIAA Journal, 2021, doi:10.2514/1.J060972, for IPDI-cured HTPB R-45M: formula
      C 213.8 H 323.0 O 4.6 N 2.3, enthalpy of formation +342 kJ/mol of that formula unit
      (+114 kJ/kg) at 298.15 K, molar mass about 2999 g/mol (342 / 2.999 = 114 checks the
      two figures against each other). The owner read the values in the paper's
      abstract. Evidence: the `HTPB` constant of `generate/propellants.py`, whose note
      is recorded in the inputs of every fixture that carries the reactant
      (`reactants[].note`), so the AP/HTPB/Al fixtures change when the definition does.
      The paper also states that the heats of formation of HTPB in the literature vary
      widely and change equilibrium results by up to 5 %; the guide
      (`docs/guide/rocket.md`) says so.

      ⚠ 2026-10-02: was the provisional definition "formula C 7.3165 H 10.3416 O 0.0674,
      enthalpy −250 cal/mol (−1046.0 J/mol) at 298.15 K, the definition used by common
      CEA front ends; to be confirmed against a cited source", used provisionally by
      `propellants.py`; now Thomas & Petersen 2021 above, because the provisional one
      carried no source. The AP/HTPB/Al fixtures were regenerated with it the same day:
      the content of 40 fixtures moved, exactly the 40 whose inputs name HTPB, and the
      other 296 moved in their provenance block only (`generatorSha256`, and
      `scriptSha256` for those `propellants.py` wrote), since the generator's own hash
      moved with the script; `regenerate.py --check` then reports every fixture
      unchanged. Consequences for the case matrix are in
      [generate/BOOT.md](generate/BOOT.md), the ⚠ notes of 2026-10-02.
- [x] 2026-09-12 — `data/thermo.inp` and `data/trans.inp` are committed verbatim from
      the nasa/cea tag `v3.3.4` (commit `4c5c612efa2002a94e3a5a1f33b1674d55c65340`), the
      release that produced the 3.3.4 wheel; the tag, the commit and the SHA-256 of both
      files are recorded in `data/NOTICE`.
- [x] 2026-09-12 — The tree's species selection for each case equals the package's
      product list recorded in the fixture inputs (`products`); checked where the tree
      selects species, not in the generator, which cannot run the tree:
      `Problems.Tests.PropellantTests.CandidateSpeciesEqualTheReferenceProductList`
      over every rocket, tp, hp and sp file (the list from the directory listing, 195
      that day). The RP-1311 examples 1 and 12 were generated with an explicit product
      list, which the package takes as given; since 2026-09-12 the fixture inputs
      record it (`only`, see the inputs paragraph), and the comparison uses it.

      ⚠ 2026-09-12: was "the generator asserts the species list equals the one `Data`
      reads", now the selecting node compares → HISTORY.md#crit-species-list
- [x] 2026-09-13 — The melting-plateau cases are generated with the station guard in
      force: the full regeneration re-solved 98 rocket solutions through the guard
      with a worst entropy residual of 8.3e-8 and no range failure, the plateau
      fixtures carry the convention values of the matrix above, and example 13
      regenerates byte-identically with its insert list recorded (the regeneration
      runs of 2026-09-13: `regenerate.py` over every kind, unchanged everywhere but
      the new and reprovenanced files; `Fixtures.Tests` green on form and
      provenance).
- [x] 2026-09-12 — Tolerances calibrated after the first full comparison; every entry
      confirmed or reworded with the reason, with the date. The tp, hp and sp kinds,
      the frozen stations, the rocket kind, the transport kind and the end-to-end
      comparison passed the table unchanged but for one calibration: `pressure` from
      1e-5 to 1e-4 relative, because the reference's throat and area-ratio pressures
      carry its iteration residual of up to 4e-5 (RP-1311 equations 6.16 and 6.25;
      3.9e-5 observed), while an assigned pressure stays exact. Evidence: the
      Equilibrium, Performance, Transport and Problems tests nodes
      (`EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`,
      `RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`); the per-kind file
      counts and worst figures → HISTORY.md#crit-tolerances-calibrated
- [x] 2026-09-14 — The shared rules of 2026-09-14: `ToleranceTable.MoleFractionField`
      picks the entry by the table's own `moleFraction` threshold, and the equilibrium
      (`Equilibrium.Tests/StateComparison.cs`), performance
      (`Performance.Tests/StationComparison.cs`) and front door
      (`Problems.Tests/ReferenceComparison.cs`) tests nodes call it instead of a
      constant and a selection line of their own; `moleFractionFloor` and
      `polishThresholdRelative` stand in the table with their derivations, and the
      execution (`Execution.Tests/GpuCpuTolerances.cs`) and front door
      (`Problems.Tests/RocketTests.cs`) tests nodes read them instead of their copies;
      `Fixtures.Tests.ToleranceTableTests.MoleFractionFieldPicksByTheThresholdAndOneUlpOnEachSide`
      proves the rule at the threshold and one ULP on each side, seen red once with the
      comparison reversed (`<` for `>=`), reverted before that test was committed, the
      evidence recorded in the test's own doc comment.
- [x] 2026-09-15 — The creation of `Provenance` in `CeaFixtures` names its arguments,
      each bound to the `generator` field its value is read from (the root's condition
      on a declared wide constructor, the row of `## Shape exceptions`): one site,
      `CeaFixtures.ReadProvenance`, fully named; covered by
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, green at
      `62cd99e`; every fixture theory of the tests nodes
      green unchanged: the full-solution fast suite (10 projects, 3014 tests) green,
      every consumer's `Bits.approved.txt` hash unmoved.

      ⚠ 2026-09-15: was one call site bound to an index of a side array, now
      `CeaFixtures.ReadProvenance` reads each field directly
      → HISTORY.md#crit-provenance-args


- [x] 2026-09-27 — The throat family (the case matrix): `tests/Fixtures/generate/throat_scan.py`
      (merged as `efe7d7e`), registered in `regenerate.py`.
      - `regenerate.py` writes the ten cases, and `regenerate.py --check` exits 0 right
        after (over all 327 fixtures of every kind).
      - The method guard (cea's own rocket throat, wherever it is sonic within 1e-4 of
        Mach 1, must match the scan's c* within 1e-5 relative) was shown red once by
        cutting the grid to exclude the true peak and by perturbing the scan's c* by
        1 % (the bracket-end swap was degenerate on a regular case and replaced).
      - The loader reads the kind (`FixtureLoadingTests.TheKindsPresentAreThoseOfTheCaseMatrix`,
        `EveryFixtureNamesTheScriptThatWroteIt` knows `cea-package-mass-flux-scan`) and
        `ToleranceTableTests.EveryStateFieldOfTheFixturesHasATolerance` covers its form
        (`[InlineData("throat")]`); `tests/Fixtures.Tests` 28/28 green.
      - `outputs.packageRocketThroat` holds `{"cStar", "mach", "pressureRatio"}` when the
        package's own solve and guard both pass, `{"guardError": "…"}` when they do not;
        no test reads either shape.

      ⚠ 2026-09-27: was "the fixture records the package's throat (c*, Mach, pressure
      ratio)", now `{"guardError"}` where the package's own guard raises
      → HISTORY.md#crit-throat-family

      ⚠ 2026-09-27: `rp1311.py`'s example 13 constants were hoisted for the import
      of `throat_scan.py`; its 63 fixtures were re-provenanced only
      → HISTORY.md#crit-throat-family

- [x] 2026-09-28 — The throat family's four F1 cases (the case matrix; the second
      hidden-defect audit's finding F1,
      `src/Performance/HISTORY.md#throat-first-maximum`): `throat_scan.py` gains
      `_first_local_max` (the first local maximum met scanning from the chamber side,
      in place of the grid's overall maximum), the `ELEMENT_MIXTURE_CASES` list and
      `element_mixture_throats`, and the guard's second accepted branch (the package's own rocket solver can converge to the same
      wrong, downstream sonic point: accepted when the scan's `p/p_c` is upstream of
      the package's and its c* is no higher).
      - `regenerate.py throat` writes all 14 cases of the family, and `regenerate.py
        --check throat` reports `fixtures: unchanged 14`.
      - Touching `throat_scan.py` re-provenanced the other 312 fixtures too (the full
        driver, 322 changed files), only `generator.*` keys moving; `Fixtures.Tests`
        34/34, `EveryFixturesGeneratorSha256MatchesTheCommittedGenerator` included.
      - The four cases' own figures: the "Measured 2026-09-28" bullet of the `throat`
        family in `generate/BOOT.md`.
      → HISTORY.md#crit-throat-f1-cases

      ⚠ 2026-09-28: the "ten cases" and "327/328 fixtures" above are the state of
      2026-09-27 and stand as written; the family now holds 14
      → HISTORY.md#crit-throat-family-count

- [x] 2026-09-28 — The throat family's three F3 cases (the case matrix; the second
      hidden-defect audit's finding F3, `src/Performance/HISTORY.md#crit-second-audit`):
      `throat_scan.py` gains the `PLATEAU_EDGE_CASES` list (points of the audit's own
      Li2O (`li-o-h`) and BeO/H2O (`be-o-h`) systems) and `plateau_edge_throats`,
      sharing `element_mixture_throats`'s method (renamed `_element_mixture_throats`,
      taking the case list as a parameter).
      - `_scan_throat`'s ternary refinement treats a trial that does not converge as
        strictly worse than any converged one (`_flux_or_negative_infinity`, and one
        retry on the converged bracket's own edge); without it `regenerate.py throat`
        raised `RuntimeError: sp solve did not converge (last_error 8)` on all 14
        h-values of the audit's 0.3 MPa Li2O band.
      - `regenerate.py throat` writes all 17 cases of the family, and `regenerate.py
        --check throat` reports `fixtures: unchanged 17`.
      - Two of the three reproduce the reference within the family's usual tolerance.
        The third, `beo-h2o-throat_pc15MPa_h-11.06875MJkg`, lands on the pinned-pair side
        of the plateau edge itself (Mach 1.011285688885813): the Performance tests'
        `StationComparison.IsPlateauEdgeDivergence` skips that one station's comparison,
        guarded on the reference's own recorded Mach (red-once evidence in the 2026-09-28
        entry of `tests/Performance.Tests/BOOT.md`); its `chamber` station still
        reproduces the reference.
      - The audit's two named 3 MPa points (h 2.20625 and 2.2125 MJ/kg) end
        `ThroatNotFound` in the tree, so the nearby h 2.2375 MJ/kg point of the same
        band, which ends `Ok`, is committed.

      Evidence: `dotnet test tests/Performance.Tests` 1418/1418
      (`src/Performance/HISTORY.md#crit-second-audit` has the F3 fact's own red-once
      record); `Fixtures.Tests` 34/34; `regenerate.py --check` (no kind filter), after the
      full-tree re-provenance, exits 0. → HISTORY.md#crit-throat-f3-cases

- [x] 2026-09-27 — The sodium case (the case matrix): `propellants.py` gains
      `sodium_hp`, one hp case (`cases/hp/nano3-rp1_of4_pc7MPa.json`) of NaNO3(a) with
      RP-1, both at 298.15 K, O/F 4, 7 MPa, generated the way `plateaus.py`'s
      `fuel_rich_hp` generates a standalone hp case, transport off.
      - The package converges (`converged: true`) to 1741.58 K; its candidate list
        carries `NaCN(II)`, the six-interval record `Thermo`'s table limit refused until
        this date, at mole fraction 0.0: the case exists for the candidate list.
      - `regenerate.py --check` exits 0 over all 328 fixtures after `regenerate.py`
        writes the one new file; the 135 other fixtures of `propellants.py` were
        re-provenanced only (2 lines each: `scriptSha256`, `generatedOn`).
      → HISTORY.md#crit-sodium-case
- [x] 2026-09-27 — Every reactant of a case given with an oxidizer-to-fuel ratio records
      its `role`, written by the generator from the oxidizer and fuel vectors it passes
      to the package (`cea_cases.describe_reactants`; never a name list).
      `regenerate.py --check` exits 0 after the regeneration, and `Fixtures.Tests`
      refuses a ratio case whose reactant lacks a role: `CeaFixtures.Load` names the
      file and the reactant's index, `MalformedFixtureTests.ARatioCaseReactantWithNoRoleIsRejected`
      proves it on a copy, shown red once, and `ARoleIsNotRequiredWithoutARatio` proves
      a role is not demanded where there is no ratio; `dotnet test tests/Fixtures.Tests`
      30/30. The full regeneration wrote 224 fixtures, and a field-by-field comparison
      found zero mismatches but `role` and the provenance keys.

      ⚠ 2026-09-27: was no `role` in the fixtures (the front door's tests guessed it from
      a hand-typed set of oxidizer names), now written by the generator
      → HISTORY.md#crit-reactant-role

- [x] 2026-09-27 — The provenance ties every fixture to the committed generator (the
      guards audit of 2026-09-26, F7): every fixture's `generator` block carries
      `generatorSha256` (`generate/BOOT.md`'s rule), and `scriptSha256` uses the same
      CRLF-to-LF normalization, so a Windows and a Linux checkout agree on both.
      - `regenerate.py` (full driver only) wrote 318 of the 328 fixtures and
        `regenerate.py --check` exits 0 immediately afterward; a field-by-field
        comparison with the three provenance keys stripped found zero mismatches.
      - `Fixtures.Tests` gains `EveryFixturesScriptSha256MatchesItsCommittedScript`,
        `EveryFixturesGeneratorSha256MatchesTheCommittedGenerator` and
        `AllFixturesCarryOneThermoLibSha256AndOneTransLibSha256`
        (`FixtureLoadingTests.cs`), each failing on an empty fixture set and shown red
        once (a zeroed `scriptSha256`; a comment added to `common.py` and not
        regenerated), reverted; `dotnet test tests/Fixtures.Tests` 33/33.
      - `Provenance` gained a twelfth parameter (`GeneratorSha256`), named at its one
        call site, and the `## Shape exceptions` row is re-measured; no `Bits*.approved.txt`
        or `Throughput*.approved.txt` moved, `PublicSurface.approved.txt` moved in the
        same commit (the `.ctor` line and `String GeneratorSha256 { get; init; }`).

      ⚠ 2026-09-26: was only the length of the three hashes checked, and `scriptSha256`
      covered the entry script, not the modules it imports
      → HISTORY.md#crit-generator-sha256
- [x] 2026-09-30 — The outputs are bound to the generator, not only the scripts (the second
      hidden-defect audit of 2026-09-28, guards part, observation O5): a hand-edited
      expected value passed every guard, and only review enforced the root's taboo on
      typed expected values.
      - The CI workflow runs `regenerate.py --check --sample` in a fresh virtual
        environment with the pinned `cea` 3.3.4 and `requirements.txt` (`.github/workflows/ci.yml`,
        step "Fixtures are bound to the generator", after the protocol lint, before the
        build). The sample is chosen by `regenerate.py`'s `build_sample` from the
        committed listing: at least one case per family and every case of the `throat`
        family (`SAMPLE_IN_FULL`).
      - `regenerate.py` gained a case-level sampling mode (`generate/writer.py`'s
        `Writer.only_cases` and `wants_case`; the stale sweep of `finish()` is off under it).
      - Red once, locally: the sampled `hp` case's `outputs.temperature` edited by +1 K
        (`cases/hp/ap-htpb-al-fuelrich_of0.5_pc7MPa.json`) made `regenerate.py --check
        --sample` exit 1 naming that file; reverted by regenerating.
      - Accepted 2026-09-30 on the CI run named under the last criterion of this group
        (run 36734450932): the step ran on `windows-latest` and passed.

      ⚠ 2026-09-30: was a Linux hosted runner and exact text, now `windows-latest` and
      a document comparison with the tolerances of `tolerances.json`
      → HISTORY.md#crit-outputs-bound

      ⚠ 2026-09-28: touching `writer.py` and `regenerate.py` re-provenanced the whole set
      (328 files, one line each) → HISTORY.md#crit-outputs-bound
- [x] 2026-09-30 — The sample of the binding step covers every script (the third audit pass of
      2026-09-28, part 2, finding 4c). The earlier sample took one case per kind
      directory, 25 of 335 files, so the outputs of `rp1311.py`, `low_temperature.py`,
      `condensed_phase_limit.py`, `retention_threshold.py` and `propellants.py`'s rocket,
      hp and tp were never compared. `build_sample` (`regenerate.py`) now reads each
      committed file's own `generator.script` field and picks one case per (script, kind)
      pair, plus every case of `SAMPLE_IN_FULL` (`throat`): 36 of 336 files over 19
      (script, kind) pairs. `check_sample_coverage` fails `--check --sample` when a pair the
      sample intended to cover produced no comparison, and when the whole sample is empty.
      - Touching `regenerate.py` re-provenanced every fixture; the full driver wrote 336
        files, each changed only in `generatorSha256` and `generatedOn`, and `--check`
        (336 unchanged) and `--check --sample` (36 unchanged) exit 0 afterward.
      - Red once, locally: `cases/hp/ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber.json`
        with `outputs.temperature` edited by +1 K, `--check --sample` exit 1 naming that
        file (changed 1, unchanged 35); restored by regenerating the kind.
      - Accepted 2026-09-30 on the same CI run: the sample of 36 files over 19 pairs
        compared at least one case per pair, the coverage check held.

      ⚠ 2026-09-30: was exact text for `--check --sample`, now documents with a
      tolerance (the last criterion); plain `--check` stays exact
      → HISTORY.md#crit-sample-every-script
- [x] 2026-09-30 — The binding step runs on the fixtures' own platform and compares with a
      tolerance (the first CI run of the binding step, on `ubuntu-latest`; the owner
      decided the same day). The two criteria above stood on an untested premise: that
      `regenerate.py` reproduces the committed files, exact text, on a hosted runner. It
      does on the reference machine (`--check`, 0 files changed) and it cannot on Linux:
      the reference is platform-dependent (`cea` aborts on three near-singular salt
      states; 253 of 333 files differ in numbers at 1e-15 relative or more, the `throat`
      family by up to 2.2e-5; the library-hash provenance keys differ by construction).
      The committed fixtures are the Windows package's, as the root's platform
      constraint says of every reference record.

      ⚠ 2026-09-30: was "`regenerate.py` reproduces the committed files, exact text, on
      a hosted runner", now only on the platform that wrote them
      → HISTORY.md#crit-binding-step

      - **Where.** The step runs on `windows-latest` only (`if: matrix.os ==
        'windows-latest'`), in a fresh virtual environment with the pinned packages, after
        the protocol lint and before the build. On Linux nothing regenerates.
      - **How.** `regenerate.py --check --sample` compares a regenerated case with the
        committed file as a document, not as text: the same keys in the same order, every
        string, boolean and null equal, every number within 1e-9 relative, the throat
        family's search-derived numbers within the `throat` row of `tolerances.json`
        (5e-5), and the provenance keys `generatedOn`, `thermoLibSha256` and
        `transLibSha256` left out, every other provenance key equal. A string that quotes
        numbers compares as its skeleton plus its tokens (`NUMBER_TOKEN` in
        `document_comparison.py`; a token with no decimal point is compared as text). Without
        `--sample` the whole set is still compared as exact text, on the reference machine.
        The comparison is `generate/document_comparison.py` (`DocumentComparison`), which
        `Writer(tolerant=True)` uses under `--check --sample` only and which reads the
        `regeneration` and `throat` rows of `tolerances.json`.
      - **Kept.** Every (script, kind) pair of the sample compares at least one case; an
        empty comparison fails; the sample takes every case of the `throat` family.
      - **Evidence.** CI run 36734450932 of `71e389c`: the step on `windows-latest` passed
        against the committed Windows files with the tolerances of `tolerances.json`
        unchanged (no row widened), was skipped on `ubuntu-latest` as designed, and the
        whole job is green on both. Locally, 2026-09-30, at `f382cd1` on the reference
        machine: `--check` and `--check --sample` exit 0, and each mutation of a committed
        file (a number by 1e-8 relative, a string, a key, an input, a throat number against
        the `throat` row, a provenance key) failed or passed as designed; the full
        regeneration wrote 336 files changed only in `generatorSha256` and `generatedOn`
        (the mutation list, the figures and the Linux measurements →
        HISTORY.md#crit-binding-step).
      - **Records.** `tests/Fixtures/generate/API.md` names the two comparison modes and
        the comment of the CI step says what it compares and where. The root's platform
        constraint is the owner's to extend if it should name the fixtures (a proposal,
        AGENTS.md §11).

- [x] 2026-10-03 — The `seeded` kind (the case matrix, "The reaction plateau"; the StateRecord
      node's pinned set): `generate/seeded.py`, registered in `regenerate.py`, with
      `solve_equilibrium`'s `seed_temperature` in `cea_cases.py`.
      - `regenerate.py` wrote the 30 cases (hp and sp, the fractions 0.1, 0.5, 0.9, −0.05 and
        1.05, at 1, 7 and 20 MPa), and `regenerate.py --check` exits 0 right after, over all
        379 fixtures. The band is measured by the script: at 7 MPa its lower end is the
        package's own hp state at 415.9 K, the upper end the tp state at 416.0 K.
      - The 18 cases inside the band all sit at T = 415.948162 K, whatever the pressure and
        the problem, with five condensed species and `cpEquilibrium == cpFrozen`; the 12 at the
        edges are single-phase (T 410.8 to 420.6 K, four condensed species) and carry real
        second-order fields.
      - The other 349 fixtures moved in `generator.generatorSha256` alone: each committed
        file and its regenerated successor, parsed, are equal in `case` and `outputs` and in
        every provenance key but that one (a script over `git diff` against `HEAD`, run once
        and not committed; the hash moved because the generator gained `seeded.py` and
        `cea_cases.py` changed).
      - The loader requires the seed and one assigned property of a seeded document
        (`CeaFixtures.RequireSeed`, `MalformedFixtureTests.AMalformedSeededDocumentIsRejected`,
        seen red once with the kind test of `RequireSeed` mutated: 5 red), and
        `FixtureLoadingTests.TheKindsPresentAreThoseOfTheCaseMatrix` and
        `ToleranceTableTests.EveryStateFieldOfTheFixturesHasATolerance` know the kind;
        `Fixtures.Tests` 42/42.
