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
| Shape | the root's code-shape constraint: type and method lines, nesting, parameters, the efferent coupling of the `src` types, stable types, the stable-dependencies direction of the `src` nodes, no `partial`, `#region` or helpers class; every exception a row of its node's `## Shape exceptions` table, measured and still needed | the C# syntax trees of the source files and the assemblies' IL; the nodes' `BOOT.md` (`ShapeTests`) | ⏳ (2026-09-14) |

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
- **The shape check measures syntax, not text** (2026-09-14): sizes, nesting,
  parameters, `partial` and `#region` from the C# syntax trees of the source files,
  coupling from the assemblies through the same IL walk as the dependency check; the
  exceptions come from the nodes' `## Shape exceptions` tables, and a row whose figure
  is below the measurement, or whose member no longer exceeds the limit, fails the
  check, so that the tables cannot go stale in either direction.

## Dependencies

None.

Outside the tree: xunit; Microsoft.CodeAnalysis.CSharp (Roslyn, the version that
parses the tree's C# 14, pinned in the project file) for the syntax trees of the shape
check (2026-09-14); Python 3.8+ on the path for the linter process; the reference
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
- The shape check's limits are the root's (its `BOOT.md`, the code-shape constraint);
  a changed number is a root decision and moves this node's reasons below with it.

## Structure

Decided 2026-09-14 (the review's F-TF-02, F-TF-08, F-TF-17). `Tree` was 428 lines with
four reflection responsibilities, and `DeclarationTests` held a grammar of `API.md`
inside a check that nested five deep. One type per file, all internal and in this
node's namespace; `PublicSurface.approved.txt` does not move, which is the proof that
nothing leaked.

| Type | Responsibility |
|---|---|
| `Tree` | where the tree is and what its nodes are: the root from `[CallerFilePath]`, the nodes by directory path, `Relative` |
| `NodeAssemblies` | the assembly each node's project builds, loaded from this project's build output; a type or an assembly → its node |
| `TypeShape` | what a type names in its declarations, its methods, its outermost declaring type, what the compiler generated |
| `IlBody` | the instructions of a method body, the operand width from the runtime's opcode table, and the types they bind to; takes methods, so that `TypeShape` → `IlBody` is one-way |
| `NodeDocuments` | what a node's own `BOOT.md` declares: the links of `## Dependencies` and the rows of `## Shape exceptions` |
| `ApiDeclarations` | the grammar of an `API.md`: its ✅ C# blocks and their declarations, the one meaning of "named in the `API.md`" for `DeclarationTests` and `CoverageTests` |
| `SourceSyntax` | the C# syntax trees of a node's source files, the build directories and generated files skipped |
| `ShapeMeasures` | the measurements of the shape check over the syntax trees and the assemblies |
| the `*Tests` classes | one fact per method: a helper yields the problems of one node or assembly, and the fact is one loop and one assertion |

Decisions taken with the review:

- **The two dependency diagnostics that restate checks of the linter stay** (F-TF-15):
  the reflection check keeps its independence from the linter, and each branch gets
  its mutation (3c, 3d below).
- **No tree-wide `GenerateDocumentationFile`** (the review's open question 6): it would
  make every public member without an XML comment an error in nodes whose contract is
  `API.md`; the misplaced comment it would have caught (F-TF-14) goes with the front
  door tests node's comparison split.
- **Size**: the root's constraint, this node's own code included.

Phase 1 (2026-09-14, this session) split `Tree`, moved `DeclarationTests`'s grammar into
`ApiDeclarations`, and flattened every fact to a problems-yielding helper plus one
assertion; `SourceSyntax` and `ShapeMeasures` are the Shape level's and stay undone
(the row below stays ⏳). Two small record types the table above does not name, because
they are data the split types carry rather than a responsibility of their own, got a
file each too, one type per file throughout: `Node` (a directory of the tree; `Tree`'s
own vocabulary) and `Instruction` (one opcode and the member its token names;
`IlBody`'s own vocabulary).

`ApiDeclarations`'s "named in the `API.md`" turned out to need more than "declared in
a ✅ block": `Execution`'s `API.md` names `EquilibriumBatchViews`, `RocketBatchViews`,
`SpeciesFunctionBatchViews` and `TransportBatchViews` only in a sentence ("public only
because ILGPU requires kernel parameter types to be … not meant to be used from
outside", found by `CoverageTests` itself under the block-only reading, first run of
this phase), never in a block. `NamesType` therefore searches the ✅-marked text as a
whole, prose and code together (a document without a status mark counts as ✅
throughout, unchanged) — the same status-mark state machine `ImplementedCsharpBlocks`
walks, kept in one place, but not narrowed to code blocks the way `DeclarationTests`'s
own reading is. Strictly tighter than the check it replaces in one respect (a name
inside a ⏳ block no longer counts, where the old whole-document regex did not
distinguish), and unchanged in the other (a name in ✅ prose still counts, as it always
did).

## Shape check

Designed 2026-09-14 (the root's code-shape constraint). Every number below means one
thing:

| Rule | Limit | Over | Definition |
|---|---|---|---|
| type lines | 400 | every type of every node | physical lines from the line of the type's modifiers or keyword to its closing brace, blank and comment lines included, attributes and the documentation comment above it excluded; a nested type counts inside its outer type and on its own |
| method lines | 60 | every method, constructor, operator, accessor with a body and local function | the same span rule for the member |
| nesting | 3 | every member body | the depth of `if` (an `else if` continues its chain), `for`, `foreach`, `while`, `do`, `switch` and `try`; a lambda or a local function continues the depth of the statement it stands in |
| parameters | 6 | every method, constructor (a record's primary constructor included), local function and delegate | the declared parameters; lambdas not counted |
| efferent coupling | 10 | every type of the `src` nodes | the distinct types of the tree a type names in its signatures and method bodies (the dependency check's walk), nested and compiler-generated types attributed to the outermost type that declares them; types outside the tree not counted |
| stable type | 100 lines at Ca ≥ 10 | every type of the `src` nodes | a type named by ten or more types of the tree spans at most 100 lines unless its node's `API.md` names it; that it holds no behaviour beyond construction and validation is left to review |
| stable dependencies | I never rises | the `src` project graph | I = Ce / (Ca + Ce) of each node over the project references; every reference points to a node whose I is not above the referrer's |
| mechanics | none | every source file | no `partial` type (one with a `[GeneratedRegex]` member excepted), no `#region`, no type whose name ends in `Helper`, `Helpers`, `Util`, `Utils` or `Common` |

The test nodes obey the size, nesting, parameter and mechanics rules, since their
support code is code; the coupling and stable-type rules apply to the `src` nodes, on
which their thresholds were calibrated. Python and the linter are outside the check.

An exception is a row of a `## Shape exceptions` table in the `BOOT.md` of the node
that holds the code:

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Kernels` | efferent coupling | 22 | the registry of the kernel entry points: one problem, scratch, result and solver type per program |

`Where` is a type's simple name (`Outer.Inner` for a nested type) or `Type.Member` for
a member, all overloads of the name together (a constructor is `Type.Type`); `Rule` is
a name of the table above; `Measured` is the figure the check measures. The check fails
when a measurement exceeds its limit without a row, when it exceeds its row's figure,
and when a row's type or member no longer exceeds the limit.

Why the numbers are what they are:

- 400 lines per type: about eight editor screens, past which no reader holds a type at
  once; the protocol decomposes a node when it "no longer has to be held in the head as
  a whole", and the user's figure agreed.
- 60 lines per method: one editor screen, so that a method's control flow is seen
  without scrolling.
- 3 levels of nesting: where the carried conditions exceed working memory, and the
  default of the usual analysers (SonarQube S134).
- 6 parameters: the lower edge of Miller's 7 ± 2, past which positional arguments of one
  type are swapped unnoticed; kernels aggregate through their `in` structs.
- 10 for efferent coupling: between the CBO thresholds of the CK-metrics literature, 9
  (Rosenberg et al., NASA SATC) and 14 (Chidamber, Darcy and Kemerer); on this tree at
  `8e36a27` it separated the tail cleanly, 120 of the 130 `src` types at or below it and
  the ten above it the known hubs.
- 10 dependants for a stable type: the tree's natural break at `8e36a27`, where every
  type with ten or more dependants was a small record, enum or struct (`Species`,
  `CaseStatus`, `ProblemKind`, `MixtureState` and the like).
- The stable-dependencies direction without an abstractness metric: numerical nodes may
  hold no interface or virtual call, so abstractness is zero throughout and the distance
  from the main sequence would degenerate to 1 − I.

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
- [x] 2026-09-14 — The support code in shape (`## Structure`, the review's F-TF-02,
      F-TF-08, F-TF-17): `Tree` split into `Tree`, `NodeAssemblies`, `TypeShape`,
      `IlBody` and `NodeDocuments` (plus `Node` and `Instruction`, the two small record
      types the split types carry); `ApiDeclarations` read by `DeclarationTests` and
      `CoverageTests`; every fact one loop and one assertion over a problems-yielding
      helper. `PublicSurface.approved.txt` unchanged (`git diff --stat` over the
      approved file empty; Surface green). The ten mutations above red again, each
      alone, with the same messages (`dotnet test tests/Protocol.Tests` after each; a
      throwaway script, not committed): (1) Coverage and Surface red, "never names
      MutationGhost1" / "no longer matches PublicSurface.approved.txt"; (2)
      Declarations red, "SpeciesTable has no member named GasCountMutated"; (3a)
      Dependencies and Lint red, "does not declare src/Data, but src/Thermo uses its
      types" / "declares nothing in canonical form"; (3b) Dependencies red, "declares
      src/Cli, but no type of src/Thermo refers to it"; (4) Surface red, "First
      difference at line 723: … 'const Int32 MutationConst = 1'"; (5) Lint red,
      "src/Ghost/API.md … is missing"; (6) the precision check red, "MutationFloat,
      Convert(value) is Single"; (7) the CUDA check red, "names
      ILGPU.Runtime.Cuda.CudaAccelerator"; (8) the static-field check red, "Counter is
      a static field that is neither const nor readonly"; (9) the namespace check red,
      "is in namespace AerospacePropellantThermodynamics.Thermo.Weird"; (10)
      Declarations and Lint red, "declares the type MutationNonexistentType under ✅,
      and no assembly of the tree has it". One pre-existing, unrelated failure ran
      alongside every check above and after: Declarations red on
      `src/Execution/API.md`'s `AcceleratorInfo`/`CudaSkippedBecause` (fixed upstream
      at `431e684`, not in this worktree's base; out of this node's subtree, left
      alone, `AGENTS.md` §3).
- [x] 2026-09-14 — The two uncovered diagnostics seen red (F-TF-15), each mutation
      alone: (3c) `tests/Fixtures/BOOT.md` linking its descendant `./generate/API.md`:
      Dependencies red, "declares its descendant tests/Fixtures/generate; a parent
      owns its children…", Lint red (warning: "declares a dependency on its own
      descendant"); (3d) `src/Data/BOOT.md` linking `../Nowhere/API.md`: Dependencies
      red, "links ../Nowhere/API.md under ## Dependencies, and no node has that
      API.md", Lint red (error: "resolves to nothing").
- [ ] Shape level green: `ShapeTests` (`No_type_spans_more_than_400_lines`,
      `No_method_spans_more_than_60_lines`, `No_control_flow_nests_deeper_than_3`,
      `No_method_takes_more_than_6_parameters`,
      `No_src_type_names_more_than_10_types_of_the_tree`,
      `Every_stable_type_is_small_or_a_contract`,
      `No_src_dependency_points_to_a_less_stable_node`,
      `No_partial_type_region_or_helpers_class`,
      `Every_shape_exception_is_measured_and_still_needed`) over the types and methods
      the check enumerates itself (the counts it measured written here when ticked).
      The nodes' `## Shape exceptions` tables transcribe the exceptions their
      `## Structure` sections declare; a violation no node declared is a finding for a
      design session, not a new row. Each fact seen red once, each mutation alone: a
      method padded to 61 lines; a fourth nesting level; a seventh parameter; an
      internal `src` type made to name an eleventh type of the tree; an internal type
      with ten dependants grown past 100 lines; a synthetic project graph with a
      reference against instability given to the same rule; a `#region`; a row's
      figure set below its measurement; a row for a member within its limits.

## Taboos

- Do not make a check pass by editing the documents when the check is wrong: fix the check.
- Do not keep a red check; fix or remove it with a declared deviation.
- Do not look up nodes by namespace segment.
- Do not use a type of another node here: the assemblies are read by reflection only,
  so that this node's `None` stays true and the checks do not depend on what they check.
