# API.md — Protocol.Tests

The node exposes nothing outward. Its contract points upward: what the tree may
consider guaranteed about the agreement between its documents and its code.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| the tree passes the language-independent linter without errors or warnings | Lint level (`LintTests`) | ✅ |
| no public surface of a library assembly changes without the snapshot moving in the same commit | Surface level (`SurfaceTests`, `PublicSurface.approved.txt`) | ✅ |
| every type a library assembly exports is named in its node's `API.md`, and every type of every assembly lives in its node's namespace | Coverage level (`CoverageTests`) | ✅ |
| every declaration under ✅ exists, the type and the member | Declarations level (`DeclarationTests`) | ✅ |
| declared dependencies match the real ones, in both directions, from signatures and method bodies | Dependencies level (`DependencyTests`) | ✅ |
| the numerical nodes hold no single-precision value or operation and no mutable static field; no node but the execution node and its tests names a CUDA type | Root invariants level (`InvariantTests`) | ✅ |
| the tree meets the root's code-shape constraint (sizes, nesting, parameters, the coupling of the `src` types, stable types, the stable-dependencies direction, no `partial`, `#region` or helpers class), every exception a measured row of its node's `## Shape exceptions` table | Shape level (`ShapeTests`) | ✅ 2026-09-15 |

What it does not guarantee: that a document tells the truth about the code it names
correctly (`AGENTS.md` §13); that a signature under ✅ matches the code (names are
checked here, signatures by the snapshot); a dependency carried only by constants,
which the compiler inlines (a node reading nothing but `const` values of a neighbour
leaves no trace in its assembly); the surface of the test assemblies; that a stable
type holds no behaviour beyond construction and validation; that a type or member a
`## Shape exceptions` row excuses is what its reason says (the Shape level reads a
row's `Where`, `Rule` and `Measured`, never its `Reason`); the target-typed creations
the named-construction rule leaves to review; that a decomposition follows the
domain's axes.

## What the tests rely on

- The tree root from `[CallerFilePath]` of `Tree.cs`; nodes from the directories
  holding both documents; assemblies by the project names, loaded from this project's
  build output.
- `PublicSurface.approved.txt` in this node: one section per library assembly, one
  line per exported type with its kind and bases, one line per public member with
  `init` told from `set`, nullable annotations, `in`/`out`/`ref`/`params`, default
  values and constant values; members the compiler writes (record equality, accessors,
  the clone helper) left out.
- The linter invoked as `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates --strict`
  from the tree root, with Python found on the path; absence of Python is a failure,
  not a skip.
- The C# syntax trees of the source files of every node whose code lives in one of the
  tree's assemblies — a node with its own project, or a project-less child node
  compiled into its nearest ancestor's (2026-09-14; child nodes, 2026-09-15) — and the
  `## Shape exceptions` tables of the nodes' `BOOT.md`.
