# API.md — Protocol.Tests

The node exposes nothing outward. Its contract points upward: what the tree may
consider guaranteed about the agreement between its documents and its code.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| the tree passes the language-independent linter without errors or warnings | Lint level (`LintTests`) | ✅ |
| every Python tool node's self-test passes (`tools/*/test_*.py`) | Tool self-tests level (`ToolSelfTestTests`) | ⏳ |
| no public surface of a library assembly changes without the snapshot moving in the same commit | Surface level (`SurfaceTests`, `PublicSurface.approved.txt`) | ✅ |
| every type a library assembly exports is named in its node's `API.md`, and every type of every assembly lives in its node's namespace | Coverage level (`CoverageTests`) | ✅ |
| every declaration under ✅ exists, the type and the member | Declarations level (`DeclarationTests`) | ✅ |
| declared dependencies match the real ones, in both directions, from signatures and method bodies | Dependencies level (`DependencyTests`) | ✅ |
| the numerical nodes hold no single-precision value or operation and no mutable static field, call only the root's allow-listed `System.Math`/`System.Double` members, and put no literal or `const` left of an ordered floating-point comparison; no node but the execution node and its tests names a CUDA type | Root invariants level (`InvariantTests`) | ✅ |
| the tree meets the root's code-shape constraint (sizes, nesting, parameters, the coupling of the `src` types, stable types, the stable-dependencies direction, no `partial`, `#region` or helpers class), every exception a measured row of its node's `## Shape exceptions` table | Shape level (`ShapeTests`) | ✅ 2026-09-15 |
| a library node's public types sit in its `API.md`'s package surface, a declared type's own section matches its reflected visibility, a friend crossing an assembly boundary is found in the target's tree contract, and every `src` assembly's `InternalsVisibleTo` names a recognised friend | Tree contract level (`TreeContractTests`) | ✅ 2026-09-15 |
| no source, build or analyzer-configuration file of the tree suppresses a diagnostic (Roslyn's own generated-code markers and every MSBuild channel that lowers a severity included), the root `Directory.Build.props`/`.targets` set the compiler and every analyzer to their maximum, every compiled source lives in a node directory, and no method reached from a kernel entry point throws, allocates or boxes | Diagnostics level (`DiagnosticsTests`) | ✅ 2026-09-25 |

What it does not guarantee: that a document tells the truth about the code it names
correctly (`AGENTS.md` §13); that a signature under ✅ matches the code (names are
checked here, signatures by the snapshot); a dependency carried only by constants,
which the compiler inlines (a node reading nothing but `const` values of a neighbour
leaves no trace in its assembly); the surface of the test assemblies; that a stable
type holds no behaviour beyond construction and validation; that a type or member a
`## Shape exceptions` row excuses is what its reason says (the Shape level reads a
row's `Where`, `Rule` and `Measured`, never its `Reason`); the target-typed creations
the named-construction rule leaves to review; that a decomposition follows the
domain's axes; the package-surface/tree-contract split of a project-less child node's
own `API.md` (root `BOOT.md`, Delivery: "Tree contracts" — the split reaches only a
node that packs its own assembly, `## Tree contract` in this node's `BOOT.md`); that a
`## Dependencies` declaration alone, without a matching real reference, justifies an
`InternalsVisibleTo` grant no code actually needs.

## What the tests rely on

- The tree root from `[CallerFilePath]` of `Tree.cs`; nodes from the directories
  holding both documents; assemblies by the project names, loaded from this project's
  build output.
- `PublicSurface.approved.txt` in this node: one section per library assembly, one
  line per exported type with its kind and bases, one line per public member with
  `init` told from `set`, nullable annotations, `in`/`out`/`ref`/`params`, default
  values and constant values; members the compiler writes (record equality, accessors,
  the clone helper) left out. `TreeContract.approved.txt` shares the same format over
  a different member floor: a declared type's public, internal and protected internal
  members, an `internal ` or `protected internal ` keyword leading a line below public
  (the guards audit's O7).
- The linter invoked as `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates --strict`
  from the tree root, with Python found on the path; absence of Python is a failure,
  not a skip.
- The C# syntax trees of the source files of every node whose code lives in one of the
  tree's assemblies — a node with its own project, or a project-less child node
  compiled into its nearest ancestor's (2026-09-14; child nodes, 2026-09-15) — read
  from each assembly's own portable PDB document table rather than a directory walk
  with name exclusions (the guards audit's F5, 2026-09-28), and the `## Shape
  exceptions` tables of the nodes' `BOOT.md`.
- A section heading of an `API.md` carrying the text `(tree contract)` marks that
  section a tree contract; a heading without it belongs to the package surface, the
  same way every heading of every node's `API.md` reads today (2026-09-15, distribution
  phase; `## Tree contract` in this node's `BOOT.md`). The split, and the facts that
  read it, apply only to a node that packs its own assembly — the eight `src` nodes and
  `Fixtures` — never to a project-less child, whose types are already visible
  throughout its own ancestor's one assembly with no `InternalsVisibleTo` grant
  possible or needed.
- The `InternalsVisibleTo` attributes of a `src` node's own assembly, read from its
  attribute data, against the nodes' `## Dependencies` and against whether the grantee
  actually names a tree-contract type of the granter.
- Every C# source file (from the compiled-sources list above, plus a narrow directory
  walk of `.github` for the one project with no `BOOT.md`/`API.md` this node's
  reflection cannot load), MSBuild project/properties/targets file, analyzer-configuration
  file and `Directory.Build.rsp` of the whole tree (2026-09-25; the guards audit's F4 and
  F5, 2026-09-28), read as text and syntax rather than through any node's own file walk,
  because a suppressed diagnostic can hide in any file of the tree, this node's own
  included.
- The call graph reached from the execution node's internal `Kernels` type, walked
  through the tree's own assemblies (the guards audit's O6, 2026-09-28): no `throw`, no
  array allocation, no allocation of a reference type and no boxing in any method it
  reaches.
