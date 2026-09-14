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

Outside the tree: xunit; Microsoft.CodeAnalysis.CSharp 5.0.0 (Roslyn, the first stable
package version built for C# 14, matching the compiler this SDK ships; pinned in the
root `Directory.Packages.props`) for the syntax trees of the shape check (2026-09-14);
Python 3.8+ on the path for the linter process; the reference
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
| `ShapeMeasures` | the size, nesting and parameter measurements of the shape check, over the syntax trees |
| `CouplingMeasures` | the coupling measurements of the shape check, over the same IL walk `DependencyTests` uses: efferent and afferent coupling per type, and Ce/Ca of each `src` node over the project graph |
| `ShapeMechanics` | the two limitless rules read from syntax: no `partial`/`#region`/banned-suffix type name, and every creation of a declared wide constructor names its arguments |
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

Phase 1 (2026-09-14, the clean-code pass) split `Tree`, moved `DeclarationTests`'s grammar into
`ApiDeclarations`, and flattened every fact to a problems-yielding helper plus one
assertion; `SourceSyntax` and `ShapeMeasures` were the Shape level's and stayed undone
(the row below stayed ⏳). Two small record types the table above does not name, because
they are data the split types carry rather than a responsibility of their own, got a
file each too, one type per file throughout: `Node` (a directory of the tree; `Tree`'s
own vocabulary) and `Instruction` (one opcode and the member its token names;
`IlBody`'s own vocabulary).

Phase 2 (2026-09-14, the measurements) wrote `SourceSyntax` and `ShapeMeasures` as
planned, extended `NodeDocuments` to read `## Shape exceptions` rows, and split the
coupling and the two limitless rules into `CouplingMeasures` and `ShapeMechanics`
(not part of the phase 1 plan; kept apart so that no file crosses the root's own size
limit and so that "read from IL" and "read from syntax" stay two files). A third small
record type joined `Node` and `Instruction` for the same reason: `ShapeException` (one
row of a `## Shape exceptions` table; `NodeDocuments`' own vocabulary). Building
`CouplingMeasures` found `TypeShape.Unwrap` folding a by-reference-to-array type
(`T[]&`, the shape of an `out T[]` parameter — a record's compiler-generated
`Deconstruct` takes one for every array-typed positional parameter) only one layer
deep, leaving `T[]` where the walk's own contract promises `T`; fixed to loop until
nothing byref, array or pointer is left, which is what let `RocketRunner` and
`EquilibriumRunner` measure 26 and 24 instead of 27 and 25 (`src/Problems/BOOT.md`'s
own note on the scratch tool's differing count, now explained rather than only
observed). A second, unrelated finding of the same build: a compiler-synthesized,
non-nested collection-expression helper type (`<>z__ReadOnlySingleElementList` and
alike) carries `[CompilerGenerated]` but no `DeclaringType` to fold into, so it passed
both the self-reference and the outside-the-tree filters; `CouplingMeasures` excludes
any compiler-generated type on either side of an edge, matching how `CoverageTests` and
`InvariantTests` already treat the same category. Neither finding moved a single
existing fact's answer: `DependencyTests`, `CoverageTests`, `InvariantTests` and
`SurfaceTests` stayed green throughout, because a byref-to-array or a free-floating
compiler-generated helper resolves to the same *node* either way — only a *type* count
sees the difference, which is this phase's own new territory.

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
| efferent coupling | 14 | every type of the `src` nodes | the distinct types of the tree a type names in its signatures and method bodies (the dependency check's walk), the nested and compiler-generated types of the naming type attributed to the outermost type that declares them, a nested type it names counted as itself, a constructed generic type counted once as its definition, an array, by-reference or pointer type counted as its element type; types outside the tree, and compiler-generated types no type declares, not counted |
| stable type | 100 lines at Ca ≥ 10 | every type of the `src` nodes | a type named by ten or more types of the tree spans at most 100 lines unless its node's `API.md` names it; that it holds no behaviour beyond construction and validation is left to review |
| stable dependencies | I never rises | the `src` project graph | I = Ce / (Ca + Ce) of each node over the project references; every reference points to a node whose I is not above the referrer's |
| mechanics | none | every source file | no `partial` type (one with a `[GeneratedRegex]` member excepted), no `#region`, no type whose name ends in `Helper`, `Helpers`, `Util`, `Utils` or `Common` |
| named construction | every argument named | every creation of a type whose constructor has a parameters row in a `## Shape exceptions` table | an object creation `new T(…)` whose written name resolves to that type, or a target-typed `new(…)` initialising a variable, field or property declared with such a name, passes every argument as `name: value`; a simple name resolves to the type of namespace N when the file's namespace is N or lies inside N, or the file imports N with a `using` directive (a global one included), and the file's own node declares no other type of that name; a qualified name resolves when its qualifier is N; any other target-typed creation is left to review |

⚠ 2026-09-14, after the measurements of phase 2: the efferent coupling row read "nested
and compiler-generated types attributed to the outermost type that declares them" without
saying on which side of a reference, and named neither arrays nor compiler-generated types
that no type declares. The scratch measurement the limit was calibrated on folded a named
nested type into its outer type as well; the dependency walk, and the measurement code
built on it, fold only the naming type's own nested types, and the two readings differ by
one on `SpeciesDatabase` (9 against 8, both under the limit). The row now states the
walk's reading, with the unwrap of arrays, by-reference and pointer types and the
exclusion of undeclared compiler-generated helpers the measurement code applies; no row's
figure and no type's standing against the limit moves.

⚠ 2026-09-14, after the measurements of phase 2: the named construction row matched a
creation by the simple name alone, and two names are declared twice in the tree:
`EquilibriumResult` (in `Equilibrium`, five parameters, and in `Problems`, a row) and
`RocketBatchViews` (in `Execution`, a row, and in `Performance.Tests`, that tests node's
own type). Read literally, the rule took six positional creations of `Equilibrium`'s type
and one of `Performance.Tests`' type for creations of the rows' types. The row now
resolves the name through the file's namespace, its `using` directives and its node's own
declarations, which gives the compiler's binding at every creation of the two names; every
creation of a row's type still falls under it.

The test nodes obey the size, nesting, parameter and mechanics rules, since their
support code is code; the coupling and stable-type rules apply to the `src` nodes, on
which their thresholds were calibrated. Python and the linter are outside the check.

An exception is a row of a `## Shape exceptions` table in the `BOOT.md` of the node
that holds the code:

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Kernels` | efferent coupling | 25 | the registry of the kernel entry points: one problem, scratch, result and solver type per program |

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
  type are swapped unnoticed; kernels aggregate through their `in` structs. A type that
  mirrors an external format or a published shape may exceed it as a row, on the root's
  condition that every creation names its arguments: a named argument cannot be swapped
  unnoticed, which is the hazard the six guard against (the named-construction rule,
  added 2026-09-14 when a scan found that condition unchecked and broken at most sites).
- 14 for efferent coupling: the maximum Sahraoui, Godin and Miceli suggest for CBO,
  above which a class's maintainability, stability and understandability suffer.
  Measured by this check's walk on the tree at `07aa9bb`, after the decompositions of
  `Equilibrium`, `Transport`, `Performance` and `Execution` and before those of `Cli`
  and `Problems`, 171 of the 184 `src` types are at or below it and none is at 15; the
  thirteen above it are the orchestrators: the composition roots and the registry the
  nodes declare, the four execution pipelines, `NewtonIteration`, `ExitStations`, and
  the hubs of `Cli` and `Problems` that their designs split or declare.

  ⚠ 2026-09-14: this bullet stood "10 for efferent coupling: between the CBO
  thresholds of the CK-metrics literature, 9 (Rosenberg et al., NASA SATC) and 14
  (Chidamber, Darcy and Kemerer); on this tree at `8e36a27` it separated the tail
  cleanly, 120 of the 130 `src` types at or below it and the ten above it the known
  hubs". Two things in it were wrong. The ten was calibrated on a textual count of the
  names in the source, while the rule is defined by the dependency check's walk, which
  also counts the types of the fields a body reads and of the members it calls; on
  that walk the kernel stages of the decomposed `Performance`, which carry their data
  explicitly as the root's no-hidden-state invariant requires, measured 12 to 16 (a
  scratch tool reproducing the walk over the build output of `07aa9bb`, before
  `ShapeTests` existed). And the attributions were wrong: the 9 is the threshold
  Shatnawi's risk-level analysis of the CK metrics derived on Eclipse, the 14 is
  Sahraoui, Godin and Miceli's, and Chidamber, Darcy and Kemerer and the NASA SATC
  guidelines associate high CBO with lower productivity and more rework without either
  figure. Found by the design session when the walk's figures came back.
- 10 dependants for a stable type: the tree's natural break at `8e36a27`, where every
  type with ten or more dependants was a small record, enum or struct (`Species`,
  `CaseStatus`, `ProblemKind`, `MixtureState` and the like). On this check's walk at
  `07aa9bb` the fifteen types with ten or more dependants are all small records, enums
  and structs of the contracts (`SpeciesTableView`, `CaseStatus`, `MixtureState`,
  `ProblemKind`, `AcceleratorInfo` and the like), and `Species` has eight.
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
      and no assembly of the tree has it". One unrelated failure ran alongside every
      check above and after, in the coder's worktree only: Declarations red on
      `src/Execution/API.md`'s `AcceleratorInfo`/`CudaSkippedBecause`, a member the
      design had declared under ✅ before the execution node's code added it (out of
      this node's subtree, left alone, `AGENTS.md` §3). On the integration branch the
      member exists since the execution node's merge, and the check holds there.

      ⚠ 2026-09-14: the parenthetical above first read "fixed upstream at `431e684`".
      That commit moved the member to a planned section while the execution node's
      coder was still at work, and `356d2ef` reverted it once the merge `8f051d8`
      brought the member: the member fixed the check, not the move.
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
      `No_src_type_names_more_than_14_types_of_the_tree`,
      `Every_stable_type_is_small_or_a_contract`,
      `No_src_dependency_points_to_a_less_stable_node`,
      `No_partial_type_region_or_helpers_class`,
      `Every_wide_constructor_is_called_with_named_arguments`,
      `Every_shape_exception_is_measured_and_still_needed`) over the types and methods
      the check enumerates itself (the counts it measured written here when ticked).
      The nodes' `## Shape exceptions` tables transcribe the exceptions their
      `## Structure` sections declare; a violation no node declared is a finding for a
      design session, not a new row. Each fact seen red once, each mutation alone: a
      type padded to 401 lines; a method padded to 61 lines; a fourth nesting level; a
      seventh parameter; an internal `src` type made to name a fifteenth type of the
      tree; an internal type with ten dependants grown past 100 lines; a synthetic
      project graph with a reference against instability given to the same rule; a
      `#region`; a row's figure set below its measurement; a row for a member within its
      limits; one argument of a declared wide constructor's creation passed by position.
      The named-construction fact also stays green when a creation of another node's
      type that shares a row type's simple name passes an argument by position (the
      resolution its definition settled on 2026-09-14).

## Taboos

- Do not make a check pass by editing the documents when the check is wrong: fix the check.
- Do not keep a red check; fix or remove it with a declared deviation.
- Do not look up nodes by namespace segment.
- Do not use a type of another node here: the assemblies are read by reflection only,
  so that this node's `None` stays true and the checks do not depend on what they check.
