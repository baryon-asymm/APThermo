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
| Shape | the root's code-shape constraint: type and method lines, nesting, parameters, the efferent coupling of the `src` types, stable types, the stable-dependencies direction of the `src` nodes, no `partial`, `#region` or helpers class; every exception a row of its node's `## Shape exceptions` table, measured and still needed | the C# syntax trees of the source files and the assemblies' IL; the nodes' `BOOT.md` (`ShapeTests`) | ✅ (2026-09-15) |

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
  (`[CallerFilePath]`).
- **A type belongs to the deepest node whose namespace equals, or prefixes at a dot
  boundary, the type's own namespace** (`AGENTS.md` §1; `NodeAssemblies.NodeOf(Type)`,
  2026-09-15): a node's own namespace is the root's (`APThermo`)
  plus its directory path from the tree root, `src` and `tests` transparent
  (`Node.Namespace`), and equals the project's own name for a node that holds one — the
  only definition a project-less child node has, since such a node compiles into its
  nearest ancestor's assembly under its own namespace (root `BOOT.md`, Constraints,
  2026-09-15) rather than owning one. A type whose own namespace carries no node of the
  tree at all (a compiler-synthesised helper no source file declares, such as
  `<PrivateImplementationDetails>` or an anonymous type, neither of which carries
  `CompilerGeneratedAttribute` for a caller to filter on, and so was never excluded that
  way) falls back to the project node of the assembly it physically sits in, exactly the
  attribution every check here read before a node's code could live in more than its own
  assembly. A type whose namespace matches no node at all, by either route, is reported
  by whichever check meets it, never silently skipped. The Coverage level's namespace
  fact (`AGENTS.md` §1) asks more than mere attribution: a type's own namespace must
  *equal* its node's, not merely sit inside a deeper, undeclared corner of an ancestor's
  — a node-shaped namespace with no node behind it stays a violation.
- **A node's own source files are the ones `SourceSyntax` walks for it**: every `*.cs`
  file under its directory except a descendant node's own subtree (project or
  project-less, `AGENTS.md` §1) — a child's files count for the child and never also for
  the parent, whether or not the child holds a project of its own. Every check that reads
  syntax (Shape's size, nesting, parameter and mechanics facts; named construction) walks
  every node whose code lives in one of the tree's assemblies (`NodeAssemblies.CodeNodes`:
  a node with its own project, or a project-less child compiled into its nearest
  ancestor's), not the project nodes alone, so a project-less node's files are measured
  once, for it, and not left unread.
- **All assemblies of the tree are searched**: every node with a project, `src/*` and
  `tests/*`, loaded by name from this project's build output; a node's own types,
  once several nodes can share one assembly, are the subset of its effective assembly's
  types the namespace attribution above resolves back to it (`NodeAssemblies.TypesOf`).
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

Decided 2026-09-14 (the review's F-TF-02, F-TF-08, F-TF-17). `Tree` was 428 physical
lines at `8e36a27` (the review's count; within 400 under the lines-of-code rule of
2026-09-14) with four reflection responsibilities, and `DeclarationTests` held a
grammar of `API.md` inside a check that nested five deep. One type per file, all
internal and in this node's namespace; `PublicSurface.approved.txt` does not move,
which is the proof that nothing leaked.

| Type | Responsibility |
|---|---|
| `Tree` | where the tree is and what its nodes are: the root from `[CallerFilePath]`, the nodes by directory path, `Relative` |
| `NodeAssemblies` | the assembly each node's project builds, loaded from this project's build output; the deepest node a type's own namespace attributes it to (2026-09-15: a project-less child node included, its own effective assembly found by walking up its ancestors); `ProjectNodeOf` (2026-09-15, child-nodes phase) is that same upward walk exposed as the nearest-project-node lookup `CouplingMeasures` reuses |
| `TypeShape` | what a type names in its declarations, its methods, its outermost declaring type, what the compiler generated |
| `IlBody` | the instructions of a method body, the operand width from the runtime's opcode table, and the types they bind to; takes methods, so that `TypeShape` → `IlBody` is one-way |
| `NodeDocuments` | what a node's own `BOOT.md` declares: the links of `## Dependencies` and the rows of `## Shape exceptions` |
| `ApiDeclarations` | the grammar of an `API.md`: its ✅ C# blocks and their declarations, the one meaning of "named in the `API.md`" for `DeclarationTests` and `CoverageTests` |
| `SourceSyntax` | the C# syntax trees of a node's source files, the build directories and generated files skipped |
| `ShapeMeasures` | the size, nesting and parameter measurements of the shape check, over the syntax trees |
| `CouplingMeasures` | the coupling measurements of the shape check, over the same IL walk `DependencyTests` uses: efferent and afferent coupling per type, and Ce/Ca of each `src` node that holds a project over the declared dependency graph, a project-less child's own declared dependencies folded into its nearest project ancestor's (`NodeAssemblies.ProjectNodeOf`, root `BOOT.md`, Constraints, 2026-09-15 child-nodes phase) |
| `ShapeMechanics` | the mechanics rule read from syntax: no `partial`/`#region`/banned-suffix type name |
| `NamedConstruction` | the named-construction rule read from syntax: the candidate types a node's `## Shape exceptions` table declares on their own constructor, and every creation, anywhere in the tree, resolving to one of them |
| `ShapeTests` | the ten facts of the Shape level: five over-limit rules matched against declared rows, stable type, stable dependencies, mechanics, named construction, and the reverse row-bookkeeping fact |
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
(the Shape row of the level table stayed planned until 2026-09-15). Two small record
types the table above does not name, because
they are data the split types carry rather than a responsibility of their own, got a
file each too, one type per file throughout: `Node` (a directory of the tree; `Tree`'s
own vocabulary) and `Instruction` (one opcode and the member its token names;
`IlBody`'s own vocabulary).

Phase 2 (2026-09-14, the measurements) wrote `SourceSyntax` and `ShapeMeasures` as
planned, extended `NodeDocuments` to read `## Shape exceptions` rows, and split the
coupling and the two limitless rules into `CouplingMeasures` and `ShapeMechanics`
(not part of the phase 1 plan; kept apart so that "read from IL" and "read from
syntax" stay two files). A third small
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
`InvariantTests` already treat the same category. Neither finding moved `DependencyTests`,
`CoverageTests` or `SurfaceTests`' answer: a byref-to-array or a free-floating
compiler-generated helper resolves to the same *node* either way — only a *type* count
sees the difference, which is this phase's own new territory.

⚠ 2026-09-15: this paragraph also named `InvariantTests` among the facts the byref-to-array
fix left unmoved, with the same "resolves to the same node" reasoning. That reasoning never
applied to it: the single-precision check compares an unwrapped type against `float` and
`Half` directly and never attributes a type to a node at all. The real reason
`InvariantTests` was untouched by this phase's fix is that its precision check
(`IsSinglePrecision`) held its own separate, one-layer-only copy of the unwrap walk,
independent of `TypeShape.Unwrap` and never reached by the fix described above; that copy
carried the very same missed cases (a by-reference-to-array parameter, a jagged array) this
paragraph's fix corrected here, undetected until R-Protocol.Tests-17 of the repair phase
made `IsSinglePrecision` reuse `TypeShape.Unwrap` itself, closing the gap between the two
copies for good.

⚠ 2026-09-15: `ShapeMechanics` held the two limitless rules (mechanics and named
construction) in one type, and this paragraph gave the file-count limit as a second
reason beside "read from IL" against "read from syntax" — but both rules read syntax;
the size limit was never the axis that told them apart. At 129 lines of code
(`ShapeMeasures.TypeLines`, well under 400), the combined type was nowhere near the
root's size limit, so the limit was not why keeping the two rules apart mattered — a
rule with no numeric limit of its own sharing a file with an unrelated rule was.
`NamedConstruction` (`Candidates`, `Creations`, and the resolution helpers
`Constructions` used) is now its own file, one type per rule read from syntax as
`## Structure` states elsewhere; `ShapeMechanics` keeps only the mechanics rule.

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

Phase 3 (2026-09-15, the two settled definitions and the facts) fixed the two things
phase 2 had left for later. `ShapeMechanics.Constructions` (now `NamedConstruction.Creations`,
the named-construction split below) matched a creation by simple name alone, so
`EquilibriumResult` (declared in `Equilibrium`, five parameters, and in
`Problems`, a row) and `RocketBatchViews` (declared in `Execution`, a row, and in
`Performance.Tests`, that node's own type) were not told apart; it now resolves a
written name the way the compiler would (namespace, enclosing namespaces, `using`
directives, the file's own node's declarations for a simple name; the qualifier alone
for a qualified one), against a `WideConstructorType` per candidate (the node plus the
simple name) rather than a bare string — a fourth small record type joining `Node`,
`Instruction` and `ShapeException`, the file each convention held. `ShapeMeasures.Span`
(since consolidated into `BoundaryTokens` and `StartLine`, this repair phase's own cut)
kept reporting the span's boundary lines; a new `CodeLines` walks the token stream
between them and counts only the lines a token touches, so a comment or blank line
inside the span no longer counts toward the 400 or the 60. `ShapeTests` then asserts
what `ShapeMeasures`, `CouplingMeasures` and `ShapeMechanics` already measured: the five
over-limit rules share one `OverLimitProblems` helper (a measurement over its limit
needs a row, a row below the measurement is the same problem restated); stable type,
stable dependencies, mechanics and named construction read directly off their own
measurement; a tenth, reverse fact re-measures every declared row and fails one that no
longer exceeds its limit or that understates a rising one. A fresh whole-tree
measurement through a temporary, uncommitted test, run once every earlier fact was
green and again once `ShapeTests` existed, found no over-limit measurement without a
row, no row below its measurement and no row past its member's need: every declared
row still matches its live figure, and no unmatched violation exists anywhere in the
tree.

Phase 4 (2026-09-15, child nodes) closes the deferral the review's R-Protocol.Tests-14
(d) left open: `SourceSyntax` already excluded a descendant node's own subtree from its
parent's file walk, project or project-less alike, but every caller asked for "every
node with a project" (`node.AssemblyName is not null`) rather than "every node whose
code lives in one of the tree's assemblies", so a project-less child's own files fell
into a walk nobody ran — excluded from the parent (correctly) and never picked up for
itself, exactly the "measured by nothing" the review named. `NodeAssemblies.NodeOf(Type)`
turns from "the node whose *assembly* built the type" into "the deepest node whose
*namespace* matches" (`Node.Namespace`, new), the one attribution the Invariants above
now define; `AssemblyOf(Node)` walks a node's own ancestor chain for the nearest project,
`CodeNodes` is every node that resolves one, and `TypesOf(Node)` is a node's own subset
of its effective assembly's types under that attribution. Every level that iterated
`NodeAssemblies.Assemblies` (the project nodes) or read `node.AssemblyName is not null`
now reads `CodeNodes` and, where it needs a node's own types rather than its whole
assembly, `TypesOf`: `CoverageTests`' two facts, `DependencyTests` (its crossings now
walk `TypesOf(node)`, not `assembly.GetTypes()`, so a shared assembly's types are not
attributed to every node that happens to compile into it), `DeclarationTests.Find`
(ranked: attributed to the node itself first, then its effective assembly, then any
assembly of the tree — the same "own assembly first" the doc comment always promised,
generalised for a node whose code need not be its own assembly), `ShapeTests` (renamed
`ProjectNodes` to `CodeNodes`) and `NamedConstruction` (`Candidates`, `Creations` and
`Resolves`, the last reading `Node.Namespace` in place of `Node.AssemblyName!`).
Building `NodeOf(Type)` on `Namespace` alone first read every type reflection reports
with no namespace of its own — `<PrivateImplementationDetails>` and the free-floating
collection-expression helpers phase 2 already found — as outside the tree, since neither
carries `CompilerGeneratedAttribute` and neither declares a namespace for a walk to match;
a whole-tree measurement dump taken before and after the change (`TypeLines`,
`MethodLines`, `Nesting`, `Parameters`, `EfferentCoupling`, `AfferentCoupling`,
`NodeCoupling`, the Coverage and Declarations sections, through a temporary,
uncommitted test, deleted again) caught the coupling figures of several test types
moving by exactly the helper types they used to name and no longer did.
`CoverageTests`' own namespace fact already skipped such a type outright (it always
excluded `type.Namespace is null`, so a type falling back needed no invitation there);
everywhere else, `NodeOf(Type)` now falls back to `NodeOf(Type.Assembly)` — the project
node of the type's own physical assembly — whenever the namespace walk finds nothing,
reproducing exactly the attribution every check read before this phase for such a type.
With the fallback in place the dump is byte-identical outside this node's own files
(`diff` empty), confirming no node's code moves on today's tree, which holds no child
node yet. `CoverageTests`' namespace fact itself changed shape rather than only scope: a
type's own namespace must now *equal* its resolved node's, not merely sit inside a
deeper, undeclared corner of an ancestor's (a `Thermo.Weird` type with no such node
still fails, the same shape as the tree's own historical mutation 9), and a mismatched
physical assembly is a second, independent failure mode the wider attribution opens
(a type whose namespace names a real node whose own effective assembly is not the one
holding the type).

## Shape check

Designed 2026-09-14 (the root's code-shape constraint). Every number below means one
thing:

| Rule | Limit | Over | Definition |
|---|---|---|---|
| type lines | 400 | every type of every node | the lines that hold code from the line of the type's modifiers or keyword to its closing brace: a line counts when it holds a token, whatever comment shares it, and a line of only white space or only comment (`//`, `///`, or a block comment's line) does not; attributes and the documentation comment above the type are outside the span; a nested type counts inside its outer type and on its own |
| method lines | 60 | every method, constructor, operator, accessor with a body and local function | the same span rule for the member |
| nesting | 3 | every member body | the depth of `if` (an `else if` continues its chain), `for`, `foreach`, `while`, `do`, `switch` and `try`; a lambda or a local function continues the depth of the statement it stands in |
| parameters | 6 | every method, constructor (a record's primary constructor included), local function and delegate | the declared parameters; lambdas not counted |
| efferent coupling | 14 | every type of the `src` nodes | the distinct types of the tree a type names in its signatures and method bodies (the dependency check's walk), the nested and compiler-generated types of the naming type attributed to the outermost type that declares them, a nested type it names counted as itself, a constructed generic type counted once as its definition, an array, by-reference or pointer type counted as its element type; types outside the tree, and compiler-generated types no type declares, not counted |
| stable type | 100 lines at Ca ≥ 10 | every type of the `src` nodes | a type named by ten or more types of the `src` nodes spans at most 100 lines, counted as the type-lines row counts them, unless its node's `API.md` names it; that it holds no behaviour beyond construction and validation is left to review |
| stable dependencies | I never rises | the `src` nodes that hold a project | I = Ce / (Ca + Ce) of each project-holding node over the dependencies its `## Dependencies` declares (held equal to the nodes its code uses by the Dependencies level; project files are not read), a project-less child's own declared dependencies joined to its nearest project ancestor's and mapped to their own project nodes, an edge that folds back onto the same project node dropped (root `BOOT.md`, Constraints, 2026-09-15 child-nodes phase); every declared dependency points to a node whose I is not above the declarer's |
| mechanics | none | every source file | no `partial` type (one with a `[GeneratedRegex]` member excepted), no `#region`, no type whose name ends in `Helper`, `Helpers`, `Util`, `Utils` or `Common` |
| named construction | every argument named | every creation of a type whose constructor has a parameters row in a `## Shape exceptions` table | an object creation `new T(…)` whose written name resolves to that type, or a target-typed `new(…)` initialising a variable, field or property declared with such a name, passes every argument as `name: value`; a simple name resolves to the type of namespace N when the file's namespace is N or lies inside N, or the file imports N with a `using` directive (a global one included), and the file's own node declares no other type of that name; a qualified name resolves when its qualifier names N followed by the row's nesting path (empty for a top-level type, `Outer` for a row declared `Outer.Inner.Inner`), written in full, or relative to the file's own namespace or one of its enclosing namespaces, or via a `using`; any other target-typed creation is left to review |

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

⚠ 2026-09-15: a qualified name's resolution read "when its qualifier is N" (the candidate's
namespace) and nothing else, which never matched two declared rows: `Records.SpeciesRecord`
and `Records.IntervalRecord` of `tests/Data.Tests`, both nested inside `internal static
class Records`, are written `Records.SpeciesRecord` and `Records.IntervalRecord` from
outside that class, a qualifier of a nested type's own enclosing type, never of the
namespace alone. A qualifier relative to an enclosing namespace escaped the same way:
`Execution.RocketBatchViews(…)` written from namespace `APThermo.
Problems` needs no `using` at all, since C# searches the enclosing namespaces of the
writing file. Both positional creations these two rows' types would therefore have gone
unreported had one existed. `WideConstructorType` now carries the row's nesting path
alongside its simple name, and a qualified name resolves against the candidate's namespace
followed by that path, written in full, relative to the file's own namespace or one of its
enclosing namespaces, or via a `using`. On the real tree the fix moves no verdict: every
existing qualified creation of a row's type already names every argument (`dotnet test
tests/Protocol.Tests` green before and after), which is what let the gap stand unnoticed.

⚠ 2026-09-14, evening: the type-lines row read "physical lines …, blank and comment
lines included". A type or a method could then be brought within its limit by deleting
its documentation or its explanatory comments, and the documentation comments of a
type's members counted toward the type's own figure. The user asked that comments not
count; the row now counts the lines that hold code, with the figures unchanged, so every
measurement can only fall and no row moves (no row declares a line rule). `ShapeMeasures`
is brought to this row together with the Shape facts.

⚠ 2026-09-15: the stable-dependencies row's Definition cell read "I = Ce / (Ca + Ce) of
each node over the project references", as if `NodeCoupling` measured the csproj
`ProjectReference` items themselves. It reads the nodes' own `## Dependencies` sections
instead (`NodeDocuments.DeclaredDependencies`), which the Dependencies level already
holds equal to the nodes whose types a node's code actually uses (`DependencyTests`);
the project files are never opened by this check. Every `src` node's declared graph and
its real `ProjectReference` list coincide at `c5aed4d`, so no I figure and no
dependency-direction verdict moves; a `ProjectReference` with no matching declared
dependency would make the two graphs differ, and this check would not notice. The row
now states what the code reads.

⚠ 2026-09-15 (repair phase, R-Protocol.Tests-9): the stable-type row's Definition cell
and `CouplingMeasures.AfferentCoupling` counted a dependant of any node of the tree, test
nodes included, against the ten-dependant threshold — a type used by nothing but its own
node's tests, or by a neighbour's tests, could read as "stable" from that use alone,
which a test assembly's own churn does not make true. The root's own stable-type sentence
already read "a type of the `src` nodes named by 10 or more types of the tree" without
saying the *naming* types were also read that widely; the coupling and stable-type rules
were calibrated on the `src` project graph throughout (this table's own heading: "Every
`src` type with an afferent coupling"), so counting a test node's use was never the
intended measure, only an unnoticed gap in the walk. `AfferentCoupling` now keeps an edge
only when its naming type's own node is a `src` node; the root's stable-type sentence is
corrected the same way, with its own warning. Ca can only fall under the narrower count,
never rise, so no type newly becomes a stable type by this fix; twenty-seven of the
fifty-three previously listed dropped back under 10 (`AcceleratorSession` among them,
`src/Execution`, old Ca 10 now 9), and the table below carries the twenty-six that remain,
re-measured the same way. No stable type over 100 lines and undocumented in its node's
`API.md` exists under either count (`Every_stable_type_is_small_or_a_contract` was green
before this fix and stays green after).

⚠ 2026-09-15 (child-nodes phase): the stable-dependencies row read "the `src` project
graph", one component per `src` node. The root `BOOT.md`'s own stable-dependencies
sentence carries the same correction, with the reason: splitting `src/Cli` into five
project-less children, four of which use `Execution` in their own code, took
`Execution`'s afferent count from 2 to 6 under the one-component-per-node reading and
its instability from 0.667 to 0.400, below `Transport`'s 0.500, turning
`No_src_dependency_points_to_a_less_stable_node` red although no dependency between
the assemblies changed — a project-less child compiles into its ancestor's assembly
(root `BOOT.md`, Constraints), so it is not a component of its own. `CouplingMeasures.NodeCoupling`
now measures the `src` nodes that hold a project only, a child's own declared
dependencies joined to its nearest project ancestor's (`NodeAssemblies.ProjectNodeOf`,
the same walk `AssemblyOf` already used) and mapped to their own project nodes; an
edge that folds back onto the same project node — a child naming its own ancestor, or
two children of one parent naming each other — is dropped, since it never crosses a
project boundary. The per-type figures (`EfferentCoupling`, `AfferentCoupling`, the
stable-type rule) are untouched: only the node-level fold changed. The node-level table
below is re-measured on the merged tree (`src/Cli` split into `Syntax`, `Documents`,
`Cases`, `Output` and `Listings`; `src/Execution/Chunks` already folded) and equals the
same dump taken at `85743de`, before the `Cli` split, with today's fix applied there
too: both give `src/Cli` Ce 7 Ca 0 (I=1.000), `src/Data` Ce 0 Ca 4 (I=0.000),
`src/Equilibrium` Ce 1 Ca 5 (I=0.167), `src/Execution` Ce 4 Ca 2 (I=0.667),
`src/Performance` Ce 2 Ca 3 (I=0.400), `src/Problems` Ce 6 Ca 1 (I=0.857), `src/Thermo`
Ce 1 Ca 6 (I=0.143), `src/Transport` Ce 3 Ca 3 (I=0.500) — none of the eight figures
moves. That the fold does not silently drop a real edge is shown by a mutation on the
child level specifically, seen red and reverted: `src/Execution/Chunks/BOOT.md`'s
`## Dependencies` given a second line, `[Problems](../../Problems/API.md)` (declared
only on the child, never on `src/Execution`'s own document), folds to `src/Execution`
depending on `src/Problems`; `No_src_dependency_points_to_a_less_stable_node` red,
"src/Execution (I=0.714) depends on src/Problems (I=0.750), which is less stable" (the
mutation raises both nodes' Ce, moving their I figures with it). No matching code
reference was added: `Execution`'s project has no reference to `Problems`'s (`Problems`
already references `Execution`, root `BOOT.md`'s dependency list), so a real one would
be a circular `ProjectReference` the build would refuse; the same declared-but-unused
shape as the node's own historical Thermo→Cli mutation for this same fact
(R-Protocol.Tests-12, above), reused here for the same reason — `NodeCoupling` reads
declared dependencies, never code, so the declaration alone is the whole input this
fact takes. Reverted; `dotnet test tests/Protocol.Tests` green again (19 passed),
`git status --short` empty.

The test nodes obey the size, nesting, parameter, mechanics and named-construction
rules, since their support code is code; the coupling and stable-type rules apply to
the `src` nodes, on which their thresholds were calibrated. Python and the linter are
outside the check.

An exception is a row of a `## Shape exceptions` table in the `BOOT.md` of the node
that holds the code:

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Kernels` | efferent coupling | 25 | the registry of the kernel entry points: one problem, scratch, result and solver type per program |

`Where` is a type's simple name (`Outer.Inner` for a nested type) or `Type.Member` for
a member, all overloads of the name together (a constructor is `Type.Type`); `Rule` is
a name of the table above; `Measured` is the figure the check measures. The check fails
when a measurement exceeds its limit without a row, when it exceeds its row's figure,
and when a row's type or member no longer exceeds the limit. The nodes' `## Shape
exceptions` tables transcribe the exceptions their `## Structure` sections declare; a
violation no node declared is a finding for a design session, not a new row.

Why the numbers are what they are:

- 400 lines of code per type: about eight editor screens of code, past which no reader
  holds a type at once; the protocol decomposes a node when it "no longer has to be held
  in the head as a whole", and the user's figure agreed. Comment and blank lines are not
  counted (2026-09-14): a limit that charges for documentation invites deleting it.
- 60 lines of code per method: about one editor screen, so that a method's control flow
  is seen without scrolling.
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

  ⚠ 2026-09-15 (repair phase, R-Protocol.Tests-9): both walks cited above counted a
  dependant of any node of the tree, test nodes included; "ten dependants" now means
  ten `src`-node dependants (the same fix as the stable-type row's Definition cell,
  above). The break the two walks found does not move by this alone: every type they
  name as being at or over ten stays a small record, enum or struct under the
  narrower count too. On the current, `src`-only walk `Species` measures 14, not
  eight — the two figures were never the same measurement to begin with, the earlier
  one taken before the tree's later decomposition added the dependants the current
  walk counts, and neither counted only `src` nodes. The live figures, `src`-only and
  dated to this repair's own commit, are the table below.
- The stable-dependencies direction without an abstractness metric: numerical nodes may
  hold no interface or virtual call, so abstractness is zero throughout and the distance
  from the main sequence would degenerate to 1 − I.

The three rules above that name only `src` types (efferent coupling, stable type, stable
dependencies) lie outside this node: a mutation of this node's own documents and code
cannot reach the `src`-node code they measure. The facts that guard them
(`No_src_type_names_more_than_14_types_of_the_tree`,
`Every_stable_type_is_small_or_a_contract`, `No_src_dependency_points_to_a_less_stable_node`)
are proved red only through synthetic inputs to their own comparison helpers (the
acceptance criterion below); the two tables here are the real inputs those same facts run
over, taken through a temporary test calling only `CouplingMeasures`, `ShapeMeasures` and
`ApiDeclarations`, never committed, so that proof is not read against a measurement that
could stay green were every coupling zero.

Every `src` type with an afferent coupling (Ca) of 10 or more, counting only `src`-node
dependants (R-Protocol.Tests-9; the table read every dependant of the tree, test nodes
included, before this repair's fix — twenty-seven of the fifty-three rows it listed then
dropped under 10 on the narrower count and are gone from the table below, none of them
over 100 lines and undocumented either way). Re-measured at the coder's commit `e3f2507`
(2026-09-15), the commit that narrowed `CouplingMeasures.AfferentCoupling` to `src`-node
dependants; no commit after it changes any type's Ce, Ca or line count:

| Node | Type | Ca | Lines | Named in `API.md` |
|---|---|---|---|---|
| `src/Cli` | `InputException` | 19 | 1 | no |
| `src/Data` | `SpeciesDatabase` | 20 | 108 | yes |
| `src/Data` | `Species` | 14 | 12 | yes |
| `src/Data` | `ElementCount` | 12 | 1 | yes |
| `src/Data` | `TemperatureInterval` | 10 | 8 | yes |
| `src/Equilibrium` | `ProblemKind` | 23 | 6 | yes |
| `src/Equilibrium` | `EquilibriumScratch` | 19 | 63 | yes |
| `src/Equilibrium` | `EquilibriumResult` | 16 | 17 | yes |
| `src/Equilibrium` | `EquilibriumProblem` | 12 | 16 | yes |
| `src/Execution` | `AcceleratorInfo` | 23 | 6 | yes |
| `src/Execution` | `AcceleratorKind` | 13 | 6 | yes |
| `src/Execution` | `EngineOptions` | 11 | 13 | yes |
| `src/Execution` | `UploadedTables` | 10 | 35 | yes |
| `src/Performance` | `FlowModel` | 18 | 6 | yes |
| `src/Performance` | `PerformanceFigures` | 15 | 9 | yes |
| `src/Performance` | `RocketResult` | 10 | 21 | yes |
| `src/Problems` | `ElementalMixture` | 21 | 68 | yes |
| `src/Problems` | `Propellant` | 12 | 21 | yes |
| `src/Problems` | `Station` | 12 | 9 | yes |
| `src/Thermo` | `CaseStatus` | 35 | 11 | yes |
| `src/Thermo` | `SpeciesTableView` | 34 | 32 | yes |
| `src/Thermo` | `MixtureState` | 26 | 22 | yes |
| `src/Thermo` | `SpeciesTable` | 15 | 75 | yes |
| `src/Transport` | `TransportFigures` | 18 | 16 | yes |
| `src/Transport` | `TransportScratch` | 11 | 111 | yes |
| `src/Transport` | `StationInputs` | 11 | 17 | no |

Every `src` node that holds a project (root `BOOT.md`, Constraints, 2026-09-15
child-nodes phase), over the dependency graph `## Dependencies` declares, a
project-less child's own dependencies folded in (eight project nodes throughout:
`Cli`, `Data`, `Equilibrium`, `Execution`, `Performance`, `Problems`, `Thermo`,
`Transport`); measured 2026-09-15 at `c5aed4d`, unmoved by the later
afferent-coupling narrowing of `e3f2507`, which touched only the per-type Ca table
above, not this node-level walk, and unmoved again by the child-nodes fold on the
merged tree at `697e48d` (`src/Cli` split into five project-less children,
`src/Execution/Chunks` already folded), verified equal to the same measurement taken
at `85743de` with the fold applied there too (this section's own ⚠, above):

| Node | Ce | Ca | I = Ce/(Ca+Ce) |
|---|---|---|---|
| `src/Cli` | 7 | 0 | 1.000 |
| `src/Data` | 0 | 4 | 0.000 |
| `src/Equilibrium` | 1 | 5 | 0.167 |
| `src/Execution` | 4 | 2 | 0.667 |
| `src/Performance` | 2 | 3 | 0.400 |
| `src/Problems` | 6 | 1 | 0.857 |
| `src/Thermo` | 1 | 6 | 0.143 |
| `src/Transport` | 3 | 3 | 0.500 |

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
      "is in namespace APThermo.Thermo.Weird"; (10)
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
- [x] 2026-09-15 — Shape level green: `ShapeTests` (`No_type_spans_more_than_400_lines`,
      `No_method_spans_more_than_60_lines`, `No_control_flow_nests_deeper_than_3`,
      `No_method_takes_more_than_6_parameters`,
      `No_src_type_names_more_than_14_types_of_the_tree`,
      `Every_stable_type_is_small_or_a_contract`,
      `No_src_dependency_points_to_a_less_stable_node`,
      `No_partial_type_region_or_helpers_class`,
      `Every_wide_constructor_is_called_with_named_arguments`,
      `Every_shape_exception_is_measured_and_still_needed`) over the types and methods
      the check enumerates itself: 422 types, 1301 methods, all ten facts green
      (`dotnet test tests/Protocol.Tests`: 19 passed). The nodes' `## Shape exceptions`
      tables transcribe the exceptions their `## Structure` sections declare; a
      violation no node declared is a finding for a design session, not a new row.

      The five over-limit facts and the reverse fact
      (`Every_shape_exception_is_measured_and_still_needed`) are themselves the
      evidence that every one of the 36 declared rows matches its live figure and that
      no over-limit measurement anywhere in the tree lacks a row: the former fail an
      unmatched measurement, the latter fails a row below its measurement or past its
      member's need, and all are green. A second source, the same whole-tree
      measurement named by its tool (`ShapeMeasures`, `CouplingMeasures`,
      `ShapeMechanics`), confirms it independently: no type or method exceeds 400/60
      lines of code, no nesting exceeds 3, no mechanics violation exists anywhere in
      the tree, every stable type is within 100 lines or named in its node's `API.md`,
      and the I ordering holds along every `src` dependency.

      Each fact seen red once, each mutation applied alone and reverted, never
      committed: (type lines) a scratch type padded to 401 lines of code, "measures 401
      for type lines, over 400, and no row ... declares it"; (method lines) a scratch
      method padded to 61 lines of code, the same message form; (nesting) four nested
      `if` statements, "measures 4 for nesting, over 3"; (parameters) a
      seven-parameter method, "measures 7 for parameters, over 6"; (mechanics) a
      `#region`, "contains a #region"; (named construction) a scratch seven-parameter
      constructor named by a temporary row of this file's own `## Shape exceptions`,
      called positionally, "created at ... without naming every argument"; (row
      bookkeeping, the reverse fact) the same temporary row first understated against
      the real seven, "states 5 for parameters, below the current measurement of 7",
      then left standing over a constructor shrunk to three parameters, "now measures 3
      for parameters, at or below the limit of 6, and no longer needs its row". The
      three rules that name only `src` types (efferent coupling, stable type, stable
      dependencies) lie outside this node, so a mutation of this node's documents and
      code cannot reach them; each was seen red instead by feeding a synthetic
      measurement straight into its private comparison helper from a temporary,
      uncommitted addition to `ShapeTests.cs`: a real internal `src/Thermo` type given a
      synthetic efferent coupling of 15, unmatched by any row; a real internal,
      undocumented type already over 100 lines given a synthetic afferent coupling of
      10; a synthetic two-node graph (a "stable" node of I=0 declared to depend on an
      "unstable" one of I=1) — each fed to `CeProblems`, `StableTypeProblems` or
      `DependencyProblems` and each producing exactly one problem.

      That these three rules also run over live, non-degenerate figures — not only the
      synthetic ones above — is shown three ways. Efferent coupling: the reverse fact
      re-measures the 14 declared efferent-coupling rows (`src/Cli` `ProblemCommand`
      30, `StatesCommand` 21, `SpeciesCommand` 15; `src/Equilibrium`
      `EquilibriumSolver` 19, `NewtonIteration` 17; `src/Execution` `Engine` 26,
      `Kernels` 25, `RocketPipeline` 23, `TransportPipeline` 22, `EquilibriumPipeline`
      21, `SpeciesFunctionPipeline` 17; `src/Problems` `RocketRunner` 26,
      `EquilibriumRunner` 24, `Solver` 22) at exactly those figures, through the same
      `CouplingMeasures.EfferentCoupling` fact 5 reads: had that measurement been
      degenerate, this re-measurement would have failed. Stable type and stable
      dependencies: the tables in `## Shape check` above are the real Ca, lines and
      `API.md`-naming, and the real Ce, Ca and I, that
      `Every_stable_type_is_small_or_a_contract` and
      `No_src_dependency_points_to_a_less_stable_node` measure — twenty-six `src`
      types at Ca ≥ 10, counting only `src`-node dependants (fifty-three before the
      repair phase's R-Protocol.Tests-9 narrowed the count the same way below; none
      over 100 lines without being named, under either count) and the eight `src`
      nodes' instability (never rising along a declared dependency), taken through a
      temporary test calling only `CouplingMeasures`, `ShapeMeasures` and
      `ApiDeclarations`, never committed.

      The named-construction fact stays green through the collision case the
      definition settled on 2026-09-14: on the real tree, six positional creations a
      simple-name match would have attributed to `Problems`' eight-parameter
      `EquilibriumResult` row (`src/Execution/Kernels.cs:95`,
      `src/Performance/StationSolve.cs:36`, `tests/Equilibrium.Tests/HostSolver.cs:112`,
      `tests/Equilibrium.Tests/InvalidInputTests.cs:33`,
      `tests/Equilibrium.Tests/KernelEqualityTests.cs:125`,
      `tests/Execution.Tests/HostSolves.cs:73`) resolve instead to `Equilibrium`'s own
      five-parameter `EquilibriumResult`, which needs no row, and are correctly not
      reported; the two rows named `RocketBatchViews` (`Execution`, `Performance.Tests`)
      and the two named `RocketResult` (`Performance`, `Problems`) each resolve to their
      own node's creation sites. The line facts stay green for a type within 400
      lines of code spanning more than 400 physical lines through comment and
      blank-line padding (verified: 353 code lines over a 413-line physical span) and
      for a method the same way at 60 (50 code lines over a 65-line physical span).

      `dotnet build APThermo.sln`: 0 warnings, 0 errors
      throughout; the fast suite green throughout
      (`dotnet test APThermo.sln --filter "Category!=LongRunning"`
      with `APTHERMO_NO_CUDA=1`: 3024 passed, up from 3014 before it, none skipped); the
      linter 0 errors, 0 warnings; `git status` clean after every mutation was reverted.
- [x] 2026-09-15 — The Shape level's non-degeneracy gaps closed (repair phase,
      R-Protocol.Tests-14), by the review's own letters, each guard seen red once with
      a temporary, uncommitted mutation and reverted:
      - **(a)** "if `SrcNodes()` is empty, facts 5 and 6 pass vacuously": the Ce fact
        (`No_src_type_names_more_than_14_types_of_the_tree`) and the stable-type fact
        (`Every_stable_type_is_small_or_a_contract`) each now assert `SrcNodes().Any()`
        before asserting zero problems, so an empty `src` node set fails loudly on its
        own rather than passing over nothing to check;
      - **(b)** "if `IsSrcNode` is false, `NodeCoupling()` is empty and fact 7 passes
        vacuously; 'src node' is written twice": the src-node predicate
        `CouplingMeasures` and `ShapeTests` each kept their own copy of, unified onto
        one definition, `Node.IsSrc`, both now read; the stable-dependencies fact
        (`No_src_dependency_points_to_a_less_stable_node`) now also asserts
        `coupling.Values.Sum(v => v.Dependencies.Count) > 0` before asserting zero
        problems;
      - **(c)** "`StableTypeProblems` silently skips a stable type whose reflection
        name has no syntax entry": it now reports such a type instead of skipping it;
      - **(e)** "no Shape fact asserts a non-empty input": every fact now does — the
        four size/nesting/parameters facts assert `MeasurementCount(rule) > 0`, the
        mechanics fact asserts at least one source file was read, named construction
        asserts `NamedConstruction.Candidates()` is not empty, and the reverse fact
        asserts the tree-wide declared-row list is not empty; (a), (b) and (c) above
        are the same guard where the population is the `src` node set specifically.

      The review's own mutation for (a) and (b), applied together as it asks (the
      shared definition means one edit reaches both): `Node.IsSrc` changed from
      `"src/"` to `"source/"`, with R-Protocol.Tests-12's Ce mutation applied alongside
      it (a scratch type, `Cli.MutationMeasureCe`, naming fifteen real types, added to
      `src/Cli` and never declared as a row). Without the (a)/(b) guards this mutation
      would leave `No_src_type_names_more_than_14_types_of_the_tree`,
      `Every_stable_type_is_small_or_a_contract` and
      `No_src_dependency_points_to_a_less_stable_node` all green — `SrcNodes()` and
      `NodeCoupling()` both empty, so none of the three would even look at `src/Cli`,
      and the real Ce violation sitting there would go unreported. With them, all
      three red on the emptiness message: `No_src_type_names_more_than_14_types_of_the_tree`
      and `Every_stable_type_is_small_or_a_contract`, "no `src` node exists in the
      tree; this fact has nothing to check"; `No_src_dependency_points_to_a_less_stable_node`,
      "the src node graph has no declared dependency edge; this fact has nothing to
      check". Both files reverted (`git status` clean); `dotnet test
      tests/Protocol.Tests` green again (19 passed).

      (c) was seen red by renaming `InputException` alone (`src/Cli`, Ca 19,
      undocumented) to `InputExceptionMUTATED` in `QualifiedName`, so it matches no
      syntax entry: "src/Cli: InputExceptionMUTATED is named by 19 types of the `src`
      nodes (a stable type) and matches no syntax entry of src/Cli/BOOT.md's own node
      to measure its lines against" — the review's own suggestion for this branch,
      `src/Execution`'s `AcceleratorSession`, no longer qualifies as a stable type at
      all after R-Protocol.Tests-9's src-only recount above took its Ca from 10 to 9,
      so this reflection-rename substitute reaches the same code path on a type that
      still does qualify; accepted as (c)'s proof for that reason.

      (e)'s named-construction and reverse-fact branches were seen red first (before
      the review's letters reached this node): `NamedConstruction.Candidates()`
      filtered to nothing, red, "no node declares a parameters row on its own
      constructor; this fact has nothing to check"; `NodeDocuments.ShapeExceptions`
      matched against a nonexistent heading, red, "no node declares a Shape exceptions
      row anywhere in the tree; this fact has nothing to re-measure". Each mutation
      applied alone, `dotnet test tests/Protocol.Tests` run, the message above the only
      failure, then reverted; green again after every revert. The whole-tree dump
      (`TypeLines`, `MethodLines`, `Nesting`, `Parameters`, `EfferentCoupling`,
      `AfferentCoupling`, `NodeCoupling`, the Coverage and Declarations sections) taken
      before and after the permanent guard changes is byte-identical from
      `EfferentCoupling` onward; the only lines that move are the `TypeLines`/`MethodLines`
      rows of `ShapeTests` itself and the line numbers of the members below the edit,
      both the expected, mechanical consequence of this node's own file growing, not a
      change of what any rule measures on the real tree.
- [x] 2026-09-15 — R-Protocol.Tests-12 (repair phase): the three rules that name only
      `src` types were, until now, proved red only through synthetic inputs fed
      straight to their own comparison helpers (the paragraph above the two tables of
      `## Shape check`), because a mutation confined to this node cannot reach the
      `src`-node code they measure. The decisions authorized a temporary, reverted
      mutation of the real `src` nodes for this evidence alone; each of the three now
      has one, applied in this node's own worktree, seen red, and reverted (`git status`
      clean, `git diff --stat` empty, after each):
      - efferent coupling: a scratch type, `Cli.MutationMeasureCe`, added to
        `src/Cli` naming fifteen distinct real types of seven other `src` nodes
        (already-declared dependencies of `Cli`, so no dependency line moved) and
        removed again; `No_src_type_names_more_than_14_types_of_the_tree` red,
        "src/Cli: MutationMeasureCe (src/Cli/MutationMeasureCe.cs:6) measures 15 for
        efferent coupling, over 14, and no row of src/Cli/BOOT.md declares it";
      - stable type: `src/Transport`'s `StationInputs` (Ca 11, undocumented, the only
        `src/Transport` row of the re-measured Ca table below the 100-line limit)
        padded with ninety `private const int` fields from 17 to 107 lines of code and
        restored; `Every_stable_type_is_small_or_a_contract` red, "src/Transport:
        StationInputs is named by 11 types of the `src` nodes (a stable type) and spans
        107 lines, over 100, without being named in src/Transport/API.md". The review's
        own suggestion, `src/Execution`'s `AcceleratorSession`, no longer qualifies:
        R-Protocol.Tests-9's src-only recount (above) took its Ca from 10 to 9, so it
        dropped out of the stable-type table before this mutation was chosen; every
        `src/Execution` type of the re-measured table is named in its `API.md`, leaving
        none to pad there undocumented, hence `StationInputs`;
      - stable dependencies: `src/Thermo/BOOT.md`'s `## Dependencies` given a second
        line, `[Cli](../Cli/API.md)`, never used by any type of `src/Thermo` (the same
        shape as the node's own historical mutation 3b, reused here for the Shape level
        rather than the Dependencies level), and removed again;
        `No_src_dependency_points_to_a_less_stable_node` red with two lines at once,
        "src/Equilibrium (I=0.167) depends on src/Thermo (I=0.250), which is less
        stable" and "src/Thermo (I=0.250) depends on src/Cli (I=0.875), which is less
        stable" (Thermo's own Ce rose 1 → 2 and Cli's Ca 0 → 1 on the declared graph);
        `DependencyTests.Every_node_declares_the_neighbours_it_uses_and_no_other` red
        alongside it, "src/Thermo/BOOT.md declares src/Cli, but no type of src/Thermo
        refers to it: the dependency went away and the document did not, or it was
        never real" — the same mutation shape genuinely breaking both levels at once,
        as the node's own history already found for the Dependencies level alone.

      `dotnet test tests/Protocol.Tests` green (20 passed) after each revert and after
      all three; `dotnet build APThermo.sln` 0 warnings, 0
      errors after each revert; the linter 0 errors, 0 warnings; no file outside this
      node carries a trace of any of the three once reverted.
- [x] 2026-09-15 — R-Protocol.Tests-13 (repair phase): eight branches the original
      ten-mutation proof (2026-09-13/14) and the Phase-3 proof (above) never exercised,
      each seen red once with a mutation applied alone and reverted, `dotnet test
      tests/Protocol.Tests` green again after each (20 passed while the temporary
      measurement dump tool was still present for the first seven; 19 for the eighth,
      added after that tool was deleted):
      - mechanics, the partial-type branch (only `#region` had been tried): a scratch
        `partial class` with no `[GeneratedRegex]` member, red, "partial type without a
        [GeneratedRegex] member";
      - mechanics, the banned-suffix branch: a scratch class named `MutationMeasureHelper`,
        red, "type name ends in the banned suffix implied by 'MutationMeasureHelper'";
      - nesting, a local function's depth reaching its enclosing member and back (the
        doc comment's "does not lower either figure", never itself tried): a method
        with no nesting of its own calling a local function with four nested `if`s, red
        on both at once, "MutationNestingLeak.Outer ... measures 4 for nesting, over 3"
        and "MutationNestingLeak.Local ... measures 4 for nesting, over 3" — the outer
        method carries the local function's own depth even though its own body holds no
        control flow;
      - the reverse fact, the "names nothing" branch (only the below-limit and
        understated branches had been tried): a temporary row for
        `MutationNonexistentType.MutationNonexistentType`, red, "the row for
        MutationNonexistentType.MutationNonexistentType (parameters) names nothing this
        fact can re-measure";
      - named construction, the target-typed `new(…)` branch (the original proof's
        "called positionally" does not say which form): `MutationWideCtor value =
        new(1, 2, 3, 4, 5, 6, 7);`, a temporary row on `MutationWideCtor.MutationWideCtor`,
        red, "MutationWideCtor created at ... without naming every argument";
      - Coverage, the ⏳-scoping `ApiDeclarations`/R-Protocol.Tests-7 gave `NamesType`
        (never itself proven red: mutation 1 of 2026-09-13 tested an undocumented type
        added to `Data`, not an existing type's only mention losing ✅ status): `src/Data/API.md`'s
        `## Database ✅` heading changed to `⏳` alone, red on five of its seven types at
        once, "src/Data/API.md never names DatabaseProvenance, which
        APThermo.Data exports" (`ElementCount`, `SpeciesPhase`,
        `SpeciesSection`, `TemperatureInterval` alongside it); `SpeciesDatabase` and
        `Species` stayed unreported, each named again in the document's other ✅
        sections, showing the fact is exact and not merely triggered by the heading
        edit itself;
      - the root's no-hidden-state invariant, the static-readonly-array branch (mutation
        8 of 2026-09-13 used a plain mutable field, not a readonly array): a scratch
        `private static readonly int[]` field added to `src/Equilibrium`, red,
        "MutationReadonlyArray.Values is a static readonly array, whose elements are
        mutable state";
      - the reverse fact, its efferent-coupling path specifically (the "names nothing",
        below-limit and understated branches proven above and at Phase 3 all happened
        to be `parameters` rows): `src/Execution/BOOT.md`'s real `Kernels` row (Ce 25,
        the tree's largest declared efferent-coupling exception) set to 24, red,
        "src/Execution/BOOT.md: Kernels's row states 24 for efferent coupling, below
        the current measurement of 25".

      Every mutation confined to a temporary, uncommitted addition (a scratch type in
      this node, `src/Cli` or `src/Equilibrium`; a temporary row of this node's own
      `## Shape exceptions`; a single status mark of `src/Data/API.md`; a single figure
      of `src/Execution/BOOT.md`'s own declared row) and reverted
      before this commit; `git status` clean and `git diff --stat` empty for every
      touched file once reverted; `dotnet build APThermo.sln`
      0 warnings, 0 errors and the linter 0 errors, 0 warnings after every revert.
- [x] 2026-09-15 — Child nodes (root `BOOT.md`, Constraints, 2026-09-15): every check
      reads one attribution (`NodeAssemblies.NodeOf(Type)`, `Node.Namespace`, `## Structure`,
      Phase 4 above), closing the review's R-Protocol.Tests-14 (d) deferral ("`.cs` files
      of a project-less node are measured by nothing"), which this `BOOT.md` had left
      unrecorded among 14's lettered guards until now.

      No figure moved on today's tree, which holds no child node yet: a whole-tree
      measurement dump (`TypeLines`, `MethodLines`, `Nesting`, `Parameters`,
      `EfferentCoupling`, `AfferentCoupling`, `NodeCoupling`, the Coverage and
      Declarations sections) taken before and after, through a temporary, uncommitted
      test (`ZzDumpTemp.cs`, deleted before this commit), is byte-identical everywhere
      outside this node's own files (`diff` between the two dumps, filtered to lines not
      naming `tests/Protocol.Tests`, empty); every difference inside this node's own
      files is the expected, mechanical consequence of its own code changing (new
      methods, moved line numbers), not a change of what any rule measures. Building the
      fallback that keeps the dump identical found a real gap first: `NodeOf(Type)` built
      on `Node.Namespace` alone returned no node for a type with no namespace of its own
      at all (`<PrivateImplementationDetails>`, the free-floating collection-expression
      helpers phase 2 already found — neither carries `CompilerGeneratedAttribute`),
      which moved several test types' efferent coupling by exactly the helper types they
      used to name (`Cli.Tests.InputDocumentTests` 12 → 11, for one); `NodeOf(Type)` now
      falls back to the project node of the type's own physical assembly when the
      namespace walk finds nothing, and the dump matched again.

      `dotnet build APThermo.sln`: 0 warnings, 0 errors;
      `APTHERMO_NO_CUDA=1 dotnet test tests/Protocol.Tests`: 19 passed; the linter 0
      errors, 0 warnings; `PublicSurface.approved.txt` unchanged
      (`git hash-object`: `35d15ae5ff2f8b189290d88d3728716e8f1b051c` before and after).

      Four proofs, each a temporary child directory `src/Cli/MutationChild/` (its own
      `BOOT.md` and `API.md`, `## Dependencies: None`) applied alone and reverted
      (`git status` clean after each):
      - **(a)** a 402-line-of-code internal type, `MutationWideType`, in namespace
        `…Cli.MutationChild`: `No_type_spans_more_than_400_lines` red, "src/Cli/MutationChild:
        MutationWideType (src/Cli/MutationChild/MutationWideType.cs:3) measures 402 for
        type lines, over 400, and no row of src/Cli/MutationChild/BOOT.md declares it" —
        naming the child node, not `src/Cli`;
      - **(b)** a public type, `MutationUndocumented`, not named in the child's own
        `API.md`: `Every_exported_type_of_a_library_assembly_is_named_in_its_nodes_api`
        red, "src/Cli/MutationChild/API.md never names MutationUndocumented, which
        APThermo.Cli.MutationChild exports (root BOOT.md,
        Taboos: no public type outside its node's API.md)", naming the child node; the
        namespace fact stayed green, since the type's own namespace resolves exactly to
        the child node that exists for it;
      - **(c)** a type of the child naming `Problems.Propellant`, with the child's own
        `## Dependencies` left `None` (its ancestor `src/Cli` already, and correctly,
        declares `Problems`): `Every_node_declares_the_neighbours_it_uses_and_no_other`
        red, "src/Cli/MutationChild/BOOT.md does not declare src/Problems, but
        src/Cli/MutationChild uses its types: MutationUsesProblems → Propellant" — naming
        the child node's own document, not the ancestor's, showing the check reads each
        node's declarations independently rather than inheriting the parent's;
      - **(d)** with (a)'s mutation in place, a temporary fact called
        `ShapeMeasures.TypeLines` directly for both `src/Cli` and `src/Cli/MutationChild`:
        the child's type is measured once, for the child (`Assert.Single`), and never
        also for the parent (`Assert.Empty`) — the same fact (a)'s own message already
        implied by naming the child, confirmed by querying the measurement itself.

      Each proof's directory and file deleted before this commit; `git status --short`
      empty; `dotnet build APThermo.sln` 0 warnings, 0 errors
      and the linter 0 errors, 0 warnings after every revert.
- [x] 2026-09-15 (child-nodes phase) — The stable-dependencies measure moved from one
      component per `src` node to one component per `src` node that holds a project
      (root `BOOT.md`, Constraints, and this node's own ⚠ above): after merging the
      `Cli` child-nodes branch (`src/Cli` split into `Syntax`, `Documents`, `Cases`,
      `Output` and `Listings`, none holding a project) onto a tree that already carried
      `src/Execution/Chunks` the same way, `No_src_dependency_points_to_a_less_stable_node`
      was red on the merged tree before this fix, "src/Execution (I=0.364) depends on
      src/Transport (I=0.500), which is less stable" (four of the five `Cli` children
      naming `Execution` inflated its afferent count under the one-component-per-node
      reading). `CouplingMeasures.NodeCoupling` now measures the eight `src` nodes that
      hold a project, folding a project-less child's own declared dependencies into its
      nearest project ancestor (`NodeAssemblies.ProjectNodeOf`, reused rather than a
      second walk) and dropping an edge that folds back onto the same project node;
      `dotnet test tests/Protocol.Tests` green after the fix (19 passed, the fact
      above among them), `dotnet test tests/Cli.Tests` green (99 passed), `dotnet build
      APThermo.sln` 0 warnings 0 errors, the linter 0 errors 0
      warnings, `PublicSurface.approved.txt` unchanged (`git hash-object`:
      `35d15ae5ff2f8b189290d88d3728716e8f1b051c`, same before and after — the change
      touches no public member). The node-level table above is the re-measurement on
      the merged tree, equal to the same measurement taken at `85743de` (before the
      `Cli` split) with today's fix applied there too, through a scratch worktree; the
      per-type figures (`CouplingMeasures.EfferentCoupling`, `AfferentCoupling`, the
      stable-type rule) are untouched by this change, since neither method was edited.
      The fold's non-degeneracy — that it does not drop a real edge, only a self-loop —
      is shown by the mutation this node's own ⚠ above records: a second `## Dependencies`
      line on `src/Execution/Chunks/BOOT.md` alone (never on `src/Execution`'s own
      document), `[Problems](../../Problems/API.md)`, folded into `src/Execution`'s Ce
      and turned the fact red, "src/Execution (I=0.714) depends on src/Problems
      (I=0.750), which is less stable"; reverted, `git status --short` empty, the fact
      green again.

## Taboos

- Do not make a check pass by editing the documents when the check is wrong: fix the check.
- Do not keep a red check; fix or remove it with a declared deviation.
- Do not look up nodes by namespace segment.
- Do not use a type of another node here: the assemblies are read by reflection only,
  so that this node's `None` stays true and the checks do not depend on what they check.
