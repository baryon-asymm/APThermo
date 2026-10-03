# API.md — Equilibrium.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Equilibrium`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| tp, hp and sp solves reproduce the reference implementation within the fixtures node's tolerance table for every fixture case | L1 over the enumerated fixture directories (`FixtureSolveTests`) | ✅ |
| condensed species enter and leave as in the reference (AP/binder/aluminium, water condensation, alumina phases at the exits) | L1 condensed cases (`CondensedSpeciesTests`) | ✅ |
| frozen mode reproduces the frozen stations of the reference rocket cases and recovers the equilibrium state at the equilibrium composition | L1 (`FrozenModeTests`) | ✅ |
| element conservation and status codes behave as the invariants state; an absent element is a mask, bit for bit | L0 (`ElementConservationTests`, `InvalidInputTests`, `AbsentElementTests`, `DenseSolverTests`) | ✅ |
| the solver gives the same bits inside a CPU-accelerator kernel as on the host | L1 kernel-equality tests (`KernelEqualityTests`) | ✅ |
| a condensed species pair pins at its cut, an enthalpy no admissible set can hold is refused rather than reported `Ok`, and an `Ok` status never leaves a positive-gain condensed candidate out — states the reference cannot reach, checked against the node's own rule | L2 (`PlateauTests`) | ✅ |
| the loop's retention verdict binds the second stage only, the switch restarts the polish, two consecutive flips hold the retained set, and a held gas stays retained; the sodium perchlorate state at Na : Cl : O = 1 : 1 : 3 that only the hold resolves converges | L0 and L2 (`ThresholdFlipTests`, `NewtonLoopStateTests`), L1 over the three flip fixtures | ✅ |
| rule B counts the gas phase as one more column of a dependent condensed inclusion and the ratio test names the species that leaves, and two condensed phases beside the gas give no combination; the potassium perchlorate with a tenth of its chlorine missing converges | L0 and L2 (`GasPhaseDependencyTests`), L1 over the four `kclo4-lean` fixtures | ✅ |
| the independent equilibrium checks skip the species of an absent element, which the mask makes no candidate | L0 (`GasPhaseDependencyTests`) | ✅ |
| no result of a fixture case changes a bit on the CPU accelerator without `Bits.approved.txt` moving in the same commit (a tripwire, not a contract) | Bits level (`BitSnapshotTests`, `Bits.approved.txt`) | ✅ |

## What the tests rely on

- The fixtures node's loader and tolerance table.
- Tables built with `Thermo` from the fixture's species list and the committed database.
- An ILGPU context with the CPU accelerator.
- `InternalsVisibleTo` from `Equilibrium` for the dense solver; a public `BatchViews`
  struct of this node as the kernel parameter of the equality test.
