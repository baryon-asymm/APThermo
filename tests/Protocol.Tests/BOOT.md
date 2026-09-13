# BOOT.md — Protocol.Tests

## Purpose

The half of the protocol's machine checks that needs reflection over the build
(`AGENTS.md` §13), written for this stack, plus the wiring of the language-independent
linter into the test set, plus the invariants of the root that need reflection. It
guards the tree against the code: the public surface against its snapshot, exported
types against `API.md`, declarations under ✅ against the assemblies, declared
dependencies against the real ones.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| Lint | the file half of the protocol, in strict mode (a warning fails too) | `tools/protocol-lint` run as a process (`LintTests`) | ✅ |
| Surface | the public surface of every library assembly of the tree equals `PublicSurface.approved.txt` | the approved snapshot (`SurfaceTests`) | ✅ |
| Coverage | every type a library assembly exports is named in the `API.md` of its node; every type of every assembly lives in the namespace of its node | the documents; the project names (`CoverageTests`) | ✅ |
| Declarations | every type and member under ✅ in any `API.md` exists | the assemblies (`DeclarationTests`) | ✅ |
| Dependencies | `## Dependencies` of every node with an assembly equals the nodes whose types its code uses, in signatures and method bodies; ancestors allowed, descendants never | the assemblies' IL (`DependencyTests`) | ✅ |
| Root invariants | double precision only and no mutable static field in the numerical nodes; no CUDA type outside the execution node and its tests | the assemblies' shapes and IL (`InvariantTests`) | ✅ |

⚠ 2026-09-13: the sketch had five levels. The root `BOOT.md` claimed three of its
invariants "checked by reflection" while no node held such a check; they are of the
same kind as the dependency check (the shapes and bodies of every type) and live here,
named in the root's invariants.

⚠ 2026-09-13: the sketch's Surface level said "every assembly of the tree". The test
assemblies' public surface is their test classes, which change with every test added;
a snapshot over them would be replaced without reading, and a tripwire nobody reads is
none. The snapshot holds the library assemblies (those not referencing xunit: the eight
of `src/` and `Fixtures`); the test assemblies are covered by the Coverage (namespace),
Declarations and Dependencies levels.

⚠ 2026-09-13: the sketch's Lint level failed "when the linter reports an error"; the
root's criterion is zero warnings, so the linter runs with `--strict` here and a
warning fails the test too.

## Invariants

- **Nodes are found by directory path** from the tree root (a directory holding both
  documents; the kit's `templates` and the build directories skipped), never by the
  last segment of a namespace; the tree root is found from the test source file
  (`[CallerFilePath]`). A type belongs to the node whose project built its assembly,
  and the Coverage level holds `AGENTS.md` §1 on top: its namespace is the project's name.
- **All assemblies of the tree are searched**: every node with a project, `src/*` and
  `tests/*`, loaded by name from this project's build output.
- **Every check has been seen red once** by the mutations named in the acceptance
  criteria, and none is ever marked skipped.
- **Links inside code blocks and backticks are not resolved**: the linter resolves
  links; this node reads only the links of `## Dependencies`.
- The IL walk reads the opcode table from the runtime (`OpCodes`), not from a copy.
- **Dependencies are read from method bodies too**: the members every instruction
  names, with the types in those members' signatures. An enum used only through its
  literals is an integer in the IL and appears in no token of its own; it is caught
  through the property or parameter it is assigned to.

## Dependencies

None.

Outside the tree: xunit; Python 3.8+ on the path for the linter process; the reference
implementation of the checks in the protocol kit (`reference/dotnet/`), adapted as its
README requires: English headings, node by path, several assemblies, parent and
children rule, links check removed (the linter has it).

## Constraints

- Part of the default test command; no accelerator is created.
- The assemblies are loaded from the build output found through the project references
  of this test project; the test project references every node's project for that
  reason only and uses none of their types (its own `## Dependencies` is `None`, and
  the Dependencies level checks it like any other node).
- A missing snapshot is written once and the test fails with instructions; the actual
  snapshot on a mismatch is `PublicSurface.actual.txt` next to the approved one,
  git-ignored, removed again when the surfaces agree.
- The numerical nodes of the root invariants are listed in
  `InvariantTests.NumericalNodes` (Thermo, Equilibrium, Performance, Transport) and the
  nodes allowed to name CUDA types in `InvariantTests.CudaNodes` (Execution and its
  tests); a listed node that is not in the tree fails the test, so the lists cannot go
  stale silently.

## Acceptance criteria

- [x] 2026-09-13 — Lint wired:
      `LintTests.The_tree_passes_the_protocol_linter_with_no_error_and_no_warning`
      runs the linter as a process in strict mode; seen red on a source directory
      without documents (mutation 5 below), on a `## Dependencies` left without links
      (3a) and on a ✅ block declaring a type no source file names (10). A member
      renamed under ✅ (2) leaves the linter green: its textual check reads type names,
      and the Declarations level is what catches members.
- [x] 2026-09-13 — Surface, Coverage, Declarations, Dependencies and the root
      invariants green on the tree:
      `SurfaceTests.The_public_surface_of_the_library_assemblies_matches_the_approved_snapshot`,
      `CoverageTests.Every_exported_type_of_a_library_assembly_is_named_in_its_nodes_api`,
      `CoverageTests.Every_type_of_every_assembly_lives_in_the_namespace_of_its_node`,
      `DeclarationTests.Every_declaration_under_a_tick_exists`,
      `DependencyTests.Every_node_declares_the_neighbours_it_uses_and_no_other`,
      `InvariantTests.Numerical_nodes_hold_no_single_precision_value_or_operation`,
      `InvariantTests.Only_the_execution_node_and_its_tests_name_cuda_types`,
      `InvariantTests.Numerical_nodes_have_no_mutable_static_field`. The first run over
      the tree gave three findings. Two were real: `SpeciesFunctionBatchViews`,
      exported by `Execution` and not named in its `API.md` (fixed there with a dated
      note), and the regex source generator's classes in a namespace of their own
      (generated code: the check now skips what is marked `GeneratedCode`, as it skips
      what the compiler marks). The third, `Cli.Tests` and `Problems.Tests` declaring
      `Equilibrium` "unused", was the check's own blindness to an enum used through its
      literals; fixed by reading the signatures of the members a body names, not the
      documents.
- [x] 2026-09-13 — Each check shown red once, each mutation applied alone to the tree
      and restored (`mutations_protocol.sh` in the session's scratchpad; the ten runs):
      (1) a public type added to `Data` and not documented: Coverage and Surface red;
      (2) `GasCount` renamed in the Thermo `API.md` under ✅: Declarations red;
      (3a) the `Data` line removed from the Thermo `BOOT.md`: Dependencies red naming
      the types that cross, Lint red on the section's form; (3b) a dependency on `Cli`
      declared in the Thermo `BOOT.md` and never used: Dependencies red; (4) a public
      constant added to `TableLimits` without updating the snapshot: Surface red, the
      first differing line named; (5) `src/Ghost/Ghost.cs` without documents: Lint red;
      (6) a `float` parameter and conversion in Thermo: the precision check red;
      (7) `CudaAccelerator` named in Thermo: the CUDA check red; (8) a mutable static
      field in Equilibrium: the static-field check red; (9) a Thermo type in a
      namespace of its own: the namespace check red; (10) a ✅ block declaring a type
      that does not exist: Declarations red, Lint red.

## Taboos

- Do not make a check pass by editing the documents when the check is wrong: fix the check.
- Do not keep a red check; fix or remove it with a declared deviation.
- Do not look up nodes by namespace segment.
- Do not use a type of another node here: the assemblies are read by reflection only,
  so that this node's `None` stays true and the checks do not depend on what they check.
