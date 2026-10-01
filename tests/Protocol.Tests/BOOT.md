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
| Root invariants | double precision only and no mutable static field in the numerical nodes; only the allow-listed `System.Math`/`System.Double` members called, and no constant left of an ordered floating-point comparison; no CUDA type outside the execution node and its tests; no call from a `src` method to an ILGPU transfer with a raw by-reference parameter (a `ref T` into host memory, 2026-10-01) | the assemblies' shapes and IL, and the semantic model of a compilation over each numerical node's sources (`InvariantTests`) | ✅ |
| Shape | the root's code-shape constraint: type and method lines, nesting, parameters, the efferent coupling of the `src` types, stable types, the stable-dependencies direction of the `src` nodes, no `partial`, `#region` or helpers class; every exception a row of its node's `## Shape exceptions` table, measured and still needed | the C# syntax trees of the source files and the assemblies' IL; the nodes' `BOOT.md` (`ShapeTests`) | ✅ (2026-09-15) |
| Tree contract | a library node's public types are named in its `API.md`'s package surface, not only its tree contract; a declared type's own section (package surface or tree contract) matches its reflected visibility; a type crossing an assembly boundary through a friend grant is found in the friend's own tree-contract section; every `InternalsVisibleTo` of a `src` assembly names a recognised friend | the assemblies' reflected visibility and IL, the nodes' `API.md` and `BOOT.md` (`TreeContractTests`) | ✅ (2026-09-15, distribution phase) |
| Diagnostics | the root's Diagnostics constraint: no source, build or analyzer-configuration file suppresses a diagnostic (Roslyn's own generated-code markers and every MSBuild severity channel, rule sets included), the root build files set the maximum, every compiled source lives in a node directory, and no kernel-reached method throws, allocates or boxes | the tree's `.cs`, project, props, targets, `.editorconfig`, `.globalconfig`, `Directory.Build.rsp` and `*.ruleset` files, and the call graph from the execution node's `Kernels` (`DiagnosticsTests`) | ✅ (2026-09-25; extended 2026-09-28 and 2026-09-29) |

⚠ 2026-09-13: was five levels, now the table above → HISTORY.md#purpose-levels

⚠ 2026-09-13: was an every-assembly snapshot, now the library ones
→ HISTORY.md#purpose-snapshot

⚠ 2026-09-13: was the linter failing on errors only, now `--strict`
→ HISTORY.md#purpose-strict

## Invariants

- **Nodes are found by directory path** from the tree root (a directory holding both
  documents; the kit's `templates` and the build directories skipped), never by the
  last segment of a namespace; the tree root is found from the test source file
  (`[CallerFilePath]`).
- **A type belongs to the deepest node whose namespace equals, or prefixes at a dot
  boundary, the type's own namespace** (`AGENTS.md` §1; `NodeAssemblies.NodeOf(Type)`):
  a node's namespace is the root's (`APThermo`) plus its directory path, `src`, `tests`
  and `samples` transparent (`Node.Namespace`); a project-less child compiles into its
  nearest ancestor's assembly under its own namespace. A type with no node namespace (a
  compiler-synthesised helper) falls back to the project node of its assembly; one
  matching no node by either route is reported, never skipped. The Coverage namespace
  fact asks more than attribution: a type's namespace must *equal* its node's.
  → HISTORY.md#invariants-attribution
- **A node's own source files** are the `*.cs` files `SourceSyntax` walks under its
  directory except a descendant node's subtree, so a child's files are measured once, for
  the child; every syntax check walks every node whose code lives in one of the tree's
  assemblies (`NodeAssemblies.CodeNodes`). **All assemblies are searched**, loaded by
  name from this project's build output; a node's own types are `NodeAssemblies.TypesOf`.
- **Every check has been seen red once** by the mutations named in the acceptance
  criteria, and none is ever marked skipped.
- **Links inside code blocks and backticks are not resolved**: the linter resolves
  links; this node reads only the links of `## Dependencies`.
- The IL walk reads the opcode table from the runtime (`OpCodes`), not from a copy.
- **Dependencies are read from method bodies too**: the members every instruction
  names, with the types in their signatures; an enum used only through its literals is
  an integer in the IL and is caught through the property or parameter it is assigned to.
- **The shape check measures syntax, not text**: sizes, nesting, parameters, `partial`
  and `#region` from the C# syntax trees, coupling from the assemblies through the same
  IL walk as the dependency check; a `## Shape exceptions` row whose figure is below the
  measurement, or whose member no longer exceeds the limit, fails the check, so the
  tables cannot go stale in either direction. → HISTORY.md#invariants-condensed

⚠ 2026-09-17: was `src` and `tests` transparent, now `samples` too
→ HISTORY.md#invariants-samples

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
  of this test project, which references every node's project for that reason only and
  uses none of their types (its `## Dependencies` is `None`, checked like any other).
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
- **Blind spots closed** (2026-09-26, the guards audit, F2, F3, F4, F8, F9), each check
  shown red once and failing on an empty set. `DiagnosticsTests` refuses in every source
  and configuration file `[GeneratedCode]`, a hand-written `[CompilerGenerated]`, a first
  line `// <auto-generated` and a `generated_code` key (F2; `TypeShape.IsCompilerGenerated`
  accepts only compiler-mangled names, containing `<`). The numerical nodes' static-state
  fact also refuses a static property with a setter and a `static readonly` field of a
  type other than a primitive, `string` or an enum (F3). A syntax fact refuses there
  `float`, `Half`, `System.Single`, `MathF` and an `f` or `F` literal suffix, beside the
  IL check (F4). `TreeContract.approved.txt` snapshots the signatures of every internal
  type under a ✅ tree-contract heading of any `API.md`, moving with it in the same commit
  (F9). Mutation (4) is proved on a public type, the Ca table dated (F8).
  → HISTORY.md#constraints-blind-spots, HISTORY.md#constraints-condensed

## Structure

Decided 2026-09-14 (the review's F-TF-02, F-TF-08, F-TF-17): one type per file, all
internal and in this node's namespace; `PublicSurface.approved.txt` does not move,
which is the proof that nothing leaked. The files hold the inventory of types, each
type's documentation comment its responsibility: the tree and attribution types
(`Tree`, `Node`, `NodeAssemblies`), the readers of method bodies, documents and sources
(`TypeShape`, `IlBody`, `NodeDocuments`, `ApiDeclarations`, `SourceSyntax`,
`CompiledSources`), the Shape level's measurement types (`ShapeMeasures`,
`CouplingMeasures`, `ShapeMechanics`, `NamedConstruction`, `WideConstructorType`) and the
`*Tests` classes, each fact one loop and one assertion over a problems-yielding helper.
→ HISTORY.md#structure-table

The review's decisions: the two dependency diagnostics restating checks of the linter
stay (F-TF-15), each branch with its mutation (3c, 3d); the size limits are the root's
constraint, this node's own code included. → HISTORY.md#structure-decisions

⚠ 2026-09-24: was "no tree-wide `GenerateDocumentationFile`", now set for every project
by the root's Diagnostics constraint → HISTORY.md#structure-decisions

Rules the phases of 2026-09-14 and 2026-09-15 left: `TypeShape.Unwrap` loops until nothing
by-reference, array or pointer is left; `CouplingMeasures` excludes any compiler-generated
type on either side of an edge; `ShapeMeasures.CodeLines` counts only lines a token
touches; "named in the `API.md`" searches the ✅-marked text whole, prose and code, so a
name in a ⏳ block does not count. → HISTORY.md#structure-phases and
HISTORY.md#structure-phases-later

⚠ 2026-09-15: was `InvariantTests` unmoved by the unwrap fix on node grounds, now it
held its own unwrap copy → HISTORY.md#structure-invariant-unwrap

⚠ 2026-09-15: was `ShapeMechanics` holding both limitless rules, now
`NamedConstruction` is its own file → HISTORY.md#structure-named-construction

## Tree contract

Designed 2026-09-15 (root `BOOT.md`, Delivery: "Tree contracts"): a node's `API.md` has
a package surface and a tree contract, marked in its section headings and checked apart
here. A heading may carry `(tree contract)`: `## Chunk buffers (tree contract) ✅`; one
without it belongs to the package surface. Unlike ✅/⏳, the mark resets at every `## `
line (`ApiDeclarations.Classify`). The split applies only to a node that packs its own
assembly (`NodeAssemblies.Assemblies`: the eight `src` nodes and `Fixtures`;
`TreeContractTests.LibraryNodes`): a project-less child's types are visible throughout
its ancestor's assembly with no grant, so facts a and c never read its document. Four
facts, `TreeContractTests` (there is no fact (d): `DeclarationTests` reads every type,
both visibilities):
- **(a)** `EveryPublicTypeOfALibraryNodeIsNamedInItsPackageSurface`: every exported type
  of a library node is named in a package-surface section of its `API.md`, not just
  "somewhere under ✅" as the Coverage level asks.
- **(b)** `ATypeNamingAFriendAssemblysInternalTypeFindsItInTheTreeContract`: a type of any
  code node naming a non-public type of another node across an assembly boundary finds
  it in the target's tree-contract section. Excluded: a self or descendant node and the
  assembly a project-less child shares with its ancestor; exempt: a test node's own
  mirrored source node and its children (`TreeContractTests.MirroredSourceNode`).
- **(c)** `EveryDeclaredTypeOfALibraryNodeMatchesItsSectionToItsVisibility`: every type a
  library node's `API.md` declares (`ApiDeclarations.DeclaredTypeSections`) is public
  only where declared in a package-surface section, and internal only where it never is.
- **(e)** `EveryInternalsVisibleToGrantOfASrcNodeNamesARecognisedFriend`: every
  `InternalsVisibleTo` of a `src` assembly (`NodeAssemblies.InternalsVisibleTo`) names one
  of four friends (root `BOOT.md`, Delivery): `ILGPURuntime`; the node's mirroring test
  assembly (`<Node>.Tests`); a node declaring the granter in its `## Dependencies`; or,
  for `tests/Benchmarks`, `tests/Harness` or another test node, one that names a
  tree-contract type of the granter (`TreeContractTests.NamesTreeContractType`).
  `APThermo.Cli` is refused unconditionally: the command line receives no grant.
  → HISTORY.md#tree-contract-condensed

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

⚠ 2026-09-14: was efferent coupling folding "nested types" unqualified, now the walk's
reading → HISTORY.md#shape-ce-reading

⚠ 2026-09-14: was named construction matched by simple name, now by compiler binding
→ HISTORY.md#shape-named-resolution

⚠ 2026-09-15: was a qualifier matching the namespace alone, now plus the nesting path
→ HISTORY.md#shape-qualified-names

⚠ 2026-09-14, evening: was physical lines, now lines of code, limits unchanged
→ HISTORY.md#shape-lines-of-code

⚠ 2026-09-15: was "over the project references", now the `## Dependencies` sections
→ HISTORY.md#shape-dependency-graph

⚠ 2026-09-15: was Ca counting any node's dependants, now `src` nodes only
→ HISTORY.md#shape-ca-src-only

⚠ 2026-09-15: was one component per `src` node, now per node holding a project
→ HISTORY.md#shape-instability-components

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

- 400 lines of code per type and 60 per method: about eight editor screens and one;
  comment and blank lines are not counted (2026-09-14), since a limit that charges for
  documentation invites deleting it. 3 levels of nesting: where the carried conditions
  exceed working memory (SonarQube S134).
- 6 parameters: the lower edge of Miller's 7 ± 2, past which positional arguments of one
  type are swapped unnoticed; a type mirroring an external format may exceed it as a row
  if every creation names its arguments (the named-construction rule, 2026-09-14).
- 14 for efferent coupling: the maximum Sahraoui, Godin and Miceli suggest for CBO; 171 of
  184 `src` types are at or below it on this check's walk, none at 15 (`07aa9bb`).
- 10 dependants for a stable type: the tree's natural break, every type with ten or more
  being a small record, enum or struct of the contracts.
- The stable-dependencies direction without an abstractness metric: numerical nodes may
  hold no interface or virtual call, so abstractness is zero throughout and the distance
  from the main sequence would degenerate to 1 − I.
  → HISTORY.md#shape-why-numbers

⚠ 2026-09-14: was a Ce limit of 10 (a textual count), now 14 (the check's walk)
→ HISTORY.md#shape-ce-limit

⚠ 2026-09-15: was "ten dependants" over every node, now `src`-node dependants
→ HISTORY.md#shape-ten-dependants

The three rules naming only `src` types (efferent coupling, stable type, stable
dependencies) lie outside this node, so a mutation of its own files cannot reach them;
their facts (`NoSrcTypeNamesMoreThan14TypesOfTheTree`, `EveryStableTypeIsSmallOrAContract`,
`NoSrcDependencyPointsToALessStableNode`) are proved red through synthetic inputs and
reverted mutations of real `src` nodes (acceptance criteria) and run over live figures:
29 `src` types at Ca ≥ 10 (2026-09-27), none over 100 lines unless named in its `API.md`,
and no dependency pointing to a less stable node. → HISTORY.md#shape-live-tables

## Diagnostics check

Designed 2026-09-24 for the root's Diagnostics constraint. A build fails on any diagnostic
that is raised; it cannot see one never raised because someone suppressed it (a pragma,
an attribute, a `NoWarn`, a lowered severity, a project overriding the root's properties).
This level reads the files that could do that, as text and syntax, skipping `bin`, `obj`,
`.git`, `.claude` and every dot directory except `.github`, which CI builds
(`.github/diagnostics/IsaProbe`) under the root's settings.
→ HISTORY.md#diagnostics-intro

⚠ 2026-09-26: was the walk's exclusions the tree walk's only, now every dot directory
but `.github` → HISTORY.md#diagnostics-walk-scope

The facts of `DiagnosticsTests`, each failing on an empty set:

1. `NoSourceFileSuppressesADiagnostic`: no C# file holds a `#pragma warning` or a
   `#nullable` directive that disables or restores a context (Roslyn directive trivia).
2. `NoSourceFileCarriesASuppressionAttribute`: no attribute named `SuppressMessage` or
   `UnconditionalSuppressMessage`, with or without the `Attribute` suffix, on any target
   (`assembly:` and `module:` included); no file named `GlobalSuppressions.cs`.
3. `NoBuildFileSuppressesOrOverridesADiagnostic`: no `.csproj`, `.props` or `.targets`
   file sets `NoWarn` or `WarningsNotAsErrors`, except that the root
   `Directory.Build.targets` resets `NoWarn` to empty. None but the root
   `Directory.Build.props` sets `TreatWarningsAsErrors`, `WarningLevel`, `Features`,
   `AnalysisLevel`, any `AnalysisMode*` or `AnalysisLevel*`, `EnforceCodeStyleInBuild`,
   `GenerateDocumentationFile`, `Nullable`, `RunAnalyzers`, `RunAnalyzersDuringBuild` or
   `EnableNETAnalyzers`. No build file, root included, sets `CodeAnalysisRuleSet`, and no
   `*.ruleset` file exists. Read as XML elements, so a comment does not count.
4. `NoAnalyzerConfigurationLowersASeverity`: every `.editorconfig` and `.globalconfig`
   gives every key ending in `.severity` the value `warning` or `error`, and every option
   value has no `:severity` suffix below warning; the root `.editorconfig` holds
   `dotnet_analyzer_diagnostic.severity = warning`.
5. `TheRootBuildRunsAtTheMaximum`: the root `Directory.Build.props` sets
   `TreatWarningsAsErrors` true, `WarningLevel` 9999, `Features` `strict`, `AnalysisLevel`
   `latest-all`, `EnforceCodeStyleInBuild` and `GenerateDocumentationFile` true; the root
   `Directory.Build.targets` sets `NoWarn` to empty. → HISTORY.md#diagnostics-facts

Each fact was shown red once by a mutation applied alone and restored (2026-09-25 to
2026-09-29) → HISTORY.md#diagnostics-red-once

**Audit fixes of 2026-09-28** (the second hidden-defect audit, guards part, F1, F3, F4,
F5, O4, O6, O7); each closes a way a defect or a suppression passed every guard.
- **The math list is an allow-list (F1).** The IL fact on the numerical nodes refuses
  every call into `System.Math` and `System.Double` except `Exp`, `Log`, `Log10`, `Pow`,
  `Sqrt`, `Abs`, `Floor` and `Ceiling` of `double`, and `double.IsNaN` and
  `double.IsNegative` inside `KernelMath` only (the root's math constraint). It reads
  static members only: the instance `Equals`, `ToString` and `CompareTo` are host-side.
- **No constant on the left of an ordered comparison (the third ILGPU defect).** A syntax
  fact over the numerical nodes' sources: no literal and no `const` left of `<`, `<=`,
  `>` or `>=` between floating-point operands, typed by the semantic model's
  `ConvertedType` (2026-09-29, 4a); the half appearing after inlining is the probe's.
- **Generated code as Roslyn defines it (F3).** The generated-code fact recognises file
  names ending in `.g.cs`, `.g.i.cs`, `.generated.cs` or `.designer.cs` or starting with
  `TemporaryGeneratedFile_` (case-insensitively), any leading-trivia comment, `/* */`
  included, with `<auto-generated` or `<autogenerated`, and the attributes and key above.
- **Every channel that lowers a severity (F4).** Fact 3 also reserves
  `CodeAnalysisTreatWarningsAsErrors`, `MSBuildTreatWarningsAsErrors` and `RunCodeAnalysis`
  to the root, and refuses a `GlobalAnalyzerConfigFiles`, `EditorConfigFiles` or `Analyzer`
  item (`Include` or `Remove`) in any build file and a `Directory.Build.rsp` anywhere.
- **The sources are the compilation's own (F5).** The syntax facts read every C# file each
  assembly was compiled from (its portable PDB's document table), a restored NuGet
  package's excepted (`CompiledSources.IsPackageOwned`); the directory walk serves only
  files no compiler reads. A compiled source in a directory without `BOOT.md` and
  `API.md` fails the tree invariant. `TypeShape` exempts a `FieldInfo` or a
  `PropertyInfo` as compiler-generated only when its name is compiler-mangled, never a
  `MethodBase` or an `EventInfo` (a record's synthesized members keep ordinary names).
- **Numerical nodes are found, not typed (O4):** generated from the root's four, child
  nodes included.
- **Kernel code allocates and throws nothing (O6).** An IL fact walks the call graph from
  every entry point of the execution node's `Kernels` through the tree's assemblies and
  refuses a `throw`, a `newarr`, a `newobj` of a reference type and a `box`; host code
  (the table builders) is not reached.
- **The tree-contract snapshot lists internal members too (O7):** `internal` and
  `protected internal` beside the public ones. → HISTORY.md#diagnostics-audit-fixes
  (F1 to F5) and HISTORY.md#diagnostics-audit-observations (O4, O6, O7)

⚠ 2026-09-28: was the math fact reading static and instance members of `System.Double`,
now static only → HISTORY.md#diagnostics-audit-fixes

⚠ 2026-09-28: was "a member" compiler-generated by mangled name for every kind, now
`FieldInfo` and `PropertyInfo` only → HISTORY.md#diagnostics-audit-f5

⚠ 2026-09-30: was "every C# file each assembly was compiled from", now minus a restored
package's → HISTORY.md#diagnostics-audit-f5

## Acceptance criteria

- [x] 2026-09-13 — Lint wired:
      `LintTests.TheTreePassesTheProtocolLinterWithNoErrorAndNoWarning` runs the linter as a
      process in strict mode; red once on mutations 5, 3a and 10 → HISTORY.md#crit-lint
- [x] 2026-09-13 — Surface, Coverage, Declarations, Dependencies and the root invariants
      green on the tree: `SurfaceTests`, `CoverageTests` (two facts), `DeclarationTests`,
      `DependencyTests`, `InvariantTests` (single precision, CUDA names, static fields);
      the first run's three findings → HISTORY.md#crit-green
- [x] 2026-09-13 — Each check shown red once, ten mutations (1) to (10) applied alone and
      restored (`mutations_protocol.sh`), among them (3a) a missing and (3b) an unused
      dependency and (4) an unsnapshotted public constant → HISTORY.md#crit-ten-mutations
- [x] 2026-09-14 — The support code in shape (`## Structure`, F-TF-02, F-TF-08, F-TF-17):
      `Tree` split, `ApiDeclarations` read by two checks, every fact one loop and one
      assertion; `PublicSurface.approved.txt` unchanged, the ten mutations red again
      → HISTORY.md#crit-support-code

  ⚠ 2026-09-14: was a member "fixed upstream at `431e684`", now by the merge `8f051d8`
  → HISTORY.md#crit-support-code-fix
- [x] 2026-09-14 — The two uncovered diagnostics seen red (F-TF-15): (3c) a link to a
      descendant, (3d) a link to an `API.md` no node has → HISTORY.md#crit-3c-3d
- [x] 2026-09-15 — Shape level green: every fact of `ShapeTests` (the test runner lists
      them: the five over-limit rules, stable type, stable dependencies, mechanics, named
      construction, the reverse row fact) over the types and methods the check enumerates
      itself, each seen red once, the three `src` rules through synthetic inputs
      → HISTORY.md#crit-shape-level
- [x] 2026-09-15 — The Shape level's non-degeneracy gaps closed (R-Protocol.Tests-14):
      every Shape fact asserts a non-empty input, each guard seen red once
      → HISTORY.md#crit-shape-gaps
- [x] 2026-09-15 — R-Protocol.Tests-12: each `src`-only rule seen red on a reverted
      mutation of a real `src` node → HISTORY.md#crit-r12
- [x] 2026-09-15 — R-Protocol.Tests-13: eight branches the first proofs never exercised
      each seen red once → HISTORY.md#crit-r13
- [x] 2026-09-15 — Child nodes: every check reads one attribution (`NodeOf(Type)`); four
      proofs on a temporary child directory seen red → HISTORY.md#crit-child-nodes
- [x] 2026-09-15 (child-nodes phase) — The stable-dependencies measure counts `src`
      nodes holding a project (`CouplingMeasures.NodeCoupling`), the fold seen red
      → HISTORY.md#crit-stable-deps
- [x] 2026-09-15 (distribution phase) — Tree contract level wired: the four facts of
      `## Tree contract` (`TreeContractTests`) green on the tree without a document
      outside this node touched, each seen red once → HISTORY.md#crit-tree-contract
- [x] 2026-09-25 — Diagnostics level: the five facts of `## Diagnostics check` green,
      each failing on an empty set and seen red once → HISTORY.md#crit-diagnostics-level
- [x] 2026-09-26 — The Diagnostics walk skips every dot directory but `.github` (the ⚠
      of that date), both scope mutations seen as stated; CI run 36229326350 of
      `a94a108` green → HISTORY.md#crit-dot-walk
- [x] 2026-09-27 — The blind spots of the guards audit (Constraints), F2, F3, F4, F8, F9:
      `NoSourceFileCarriesAGeneratedCodeMarker`, `NumericalNodesHaveNoMutableStaticField`,
      `NumericalNodeSourcesNameNoSinglePrecisionSyntax`,
      `TreeContractSnapshotTests.TheTreeContractSignaturesMatchTheApprovedSnapshot`, each
      mutation seen red once, no `Bits*` or `Throughput*` record moved
      → HISTORY.md#crit-blind-spots
- [x] 2026-09-27 — No `Math.Min` or `Math.Max` in the numerical nodes:
      `InvariantTests.NumericalNodesCallNoMathMinOrMaxAndNoNanOrNegativeCheckOutsideKernelMath`,
      seen red two ways → HISTORY.md#crit-min-max

  ⚠ 2026-09-28: was a fact matching `System.Math` and two names, now an allow-list
  → HISTORY.md#crit-min-max-superseded
- [x] 2026-09-28 — The audit fixes of 2026-09-28, each fact seen red once by the audit's
      probe: `NumericalNodesCallOnlyTheAllowedMathAndDoubleMembers`,
      `NoSourceFileCarriesAGeneratedCodeMarker`, `NoBuildFileSuppressesOrOverridesADiagnostic`,
      `KernelReachedMethodsAllocateNothingAndThrowNothing`,
      `EveryCompiledSourceLivesInANodeDirectory`; 35/35 at `7da07dc` → HISTORY.md#crit-audit-fixes
- [x] 2026-09-29 — The third audit pass (4a, 4b): the integer constant on the left,
      `NumericalNodeSourcesPutNoConstantLeftOfAnOrderedFloatingComparison`, and the rule
      sets, `NoBuildFileSuppressesOrOverridesADiagnostic`, each seen red on the audit's
      own mutation; 35/35 at `0008a45` → HISTORY.md#crit-third-pass
- [x] 2026-09-30 — The compiled-source walk skips restored NuGet packages' sources:
      `CompiledSourcesTests.OnlyASourceBelowAPackageFolderIsThePackages`; 36 of 36 on
      Windows and WSL2 with the cache inside the tree → HISTORY.md#crit-package-sources

  ⚠ 2026-09-30: was the in-tree cache `.nuget-packages` "git-ignored", now it is not
  → HISTORY.md#crit-package-gitignore
- [x] 2026-10-01 — No `src` method passes host memory to ILGPU by reference (the root's
      fourth ILGPU hazard):
      `InvariantTests.NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference`, green at
      the fix (37 of 37), red once on the unfixed `ChunkBuffer.cs` and on an empty walk
      → HISTORY.md#crit-ilgpu-byref

  ⚠ 2026-10-01: was "a by-reference parameter", now one whose element type is not
  `Span<T>` or `ReadOnlySpan<T>` → HISTORY.md#crit-ilgpu-byref-wording

## Taboos

- Do not make a check pass by editing the documents when the check is wrong: fix the check.
- Do not keep a red check; fix or remove it with a declared deviation.
- Do not look up nodes by namespace segment.
- Do not use a type of another node here: the assemblies are read by reflection only,
  so that this node's `None` stays true and the checks do not depend on what they check.
