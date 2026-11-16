# API.md — Problems.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Problems` and, through it, about the system.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every rocket fixture (the four reference propellants in shifting and frozen flow, with and without transport, and the RP-1311 rocket examples) reproduces the reference implementation end to end within the tolerance table, the transport fields on the tree's own composition | L1, L2 (`RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`) | ✅ |
| every tp, hp and sp fixture reproduces the reference from its propellant, and as a state record in a batch over the union of elements of its group | L1, L2 (`EquilibriumTests`) | ✅ |
| propellant definitions are turned into mass fractions, element moles, enthalpy and candidate species exactly as the reference does (the ratio path within the reference's single precision) | L0 (`PropellantTests`) | ✅ |
| a ratio and pressure product given as one batch over mixtures equals its cases solved one by one, an elemental mixture equals its propellant, identical problems give identical results alone and in one call, each bit for bit | L2 (`RocketTests`) | ✅ |
| invalid inputs are rejected by name or index before any kernel runs; a failing station is a status, not an exception | L0, L2 (`RejectionTests`, `RocketTests.AFailingStationIsAStatusAndNotAnException`) | ✅ |
| a composition that does not weigh one kilogram with the database's atomic weights within the front door's tolerance is refused through every front door, naming the record, the mass in grams and the tolerance; one that does is solved | L0 (`RejectionTests.ACompositionThatDoesNotWeighOneKilogramIsRejectedWithItsMassAndTheTolerance`, `AReactantRecordWhoseMolarMassContradictsItsFormulaIsCaughtAtTheSolve`) | ✅ |
| the tolerance a mixture declares is the one applied, and the mass of every mixture is reported and equals the sum over the database's atomic weights; every fixture's recorded element moles weigh one kilogram within 1.7e-5 | L0 (`RejectionTests.TheToleranceAMixtureDeclaresIsTheOneApplied`, `PropellantTests.TheRecordedElementMolesOfEveryFixtureWeighOneKilogramWithinTheDerivationFigure`, `ResultsCarryTheMassOfTheirMixture`) | ✅ |
| a condensed record cut by the species table is reported once under its database name, an assigned enthalpy inside the `ALN(L)` gap solves, and a sweep across the alumina plateau stays on the isentrope | L2 (`SplitRecordTests`) | ✅ 2026-09-13 (recorded 2026-09-14) |
| a state record with exits solves as its rocket case; the rules of the record's shape refuse by index; a batch mixing transport and none equals each problem alone; a ratio and pressure product as one batch equals its cases one by one; every method of a disposed solver refuses | L2 (`RocketTests`, `RejectionTests`, the contract facts of 2026-09-14) | ✅ 2026-09-14 |
| the front door's result of every fixture is bit for bit what it was before the front door's decomposition | Bits (`BitSnapshotTests`, `Bits.approved.txt`) | ✅ 2026-09-14 |

The generated public instance-property and field inventory checks failed-station
publication, including `NoGasPhase`, and rejects unsupported members explicitly.
`StationFieldInventoryTests` separately detects stale state and performance fields.

⚠ 2026-11-16: the first inventory filtered to numeric properties and could miss a
new nonnumeric member. It now enumerates the whole public instance shape first.
`StationFieldInventoryTests.InventoryRejectsNewUnsupportedMembers` proves a supported
shape and detects added nonnumeric properties and fields; with `StationBoundaryTests`
the focused set passes 5/5. Mutations reinstating each omission fail (2026-11-16).

## What the tests rely on

- The fixtures node's loader and tolerance table, and the caveats of the reference's
  fields recorded in its BOOT.md.
- The committed database under `data/`.
- The CPU accelerator through the solver's engine options, and a CPU engine of the
  execution node to evaluate the transport solver on the reference's composition.
- `Bits.approved.txt` in this node: one line per fixture, the fixture path and the
  SHA-256 of the raw bits of its front-door result (2026-09-14).

## Boundary applicability witnesses ✅

`ApplicabilityMetadataTests` checks the A37 sidecar's committed data/source-fixture
hashes, generated raw/table fit bounds and rejects missing or corrupted metadata.
`ApplicabilityPolicyTests` consumes its exact inputs through the public solver:
graphite is present at 300 K and absent at 299.99 K with default products; ice is
admitted below its raw fit under the open-below rule; frozen flow retains alumina
at an `Ok` exit below the condensate fit. These establish the disclosed model
policy and diagnostic interpretation, not the physical validity of extrapolation.
