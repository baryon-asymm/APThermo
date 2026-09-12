# AGENTS.md — protocol for an agent working with the project tree

> This file is a **protocol**, not a description of the project. It sets the rules by
> which an agent handles the project tree: the structure of the tree, access rights,
> working modes, the order in which the tree is built, the readiness criterion, and
> what of all this the machine checks.
>
> The protocol is invariant with respect to the particular project. The subject-matter
> content of the levels lives in `BOOT.md` and `API.md`, **not** here.
>
> This file exists **only at the root** of the tree and changes only when the
> methodology is revised, never for the convenience of a single task.
>
> Version 3. Versions 1 and 2 grew in the PastyPropellant repository (August –
> September 2026): the first on a port of someone else's model, where the tree was
> built together with the code; the second on the reconstruction of a 52-node tree
> around existing code. Everything added in the third version was lived through there
> first and only then written down here; the list of changes is in Appendix B. Why the
> protocol is arranged this way is in the `CONCEPT.md` of the kit this file is taken
> from; here there are only rules. This copy is the English rendering of the kit's
> original; article numbers and headings are unchanged.

---

## 1. Project model: a tree of directories

The project is a tree of directories. Every source-code directory is a **node** of the
tree and represents one **level of abstraction**.

**Tree invariant:** every source-code directory must hold `BOOT.md` and `API.md`.
There is no code directory without this pair. The boundary of a node is literally the
boundary of the directory.

A directory counts as a source-code directory when it is:

- a directory holding at least one code file or a build manifest (`*.csproj`,
  `package.json`, `pyproject.toml`, `Cargo.toml`, `go.mod`, …);
- the root of the tree, always;
- any directory that already holds `BOOT.md` or `API.md`: in greenfield the pair is
  born before the code (§9), and the node exists from that moment.

A directory without code, manifest or documents, a purely grouping one (`src/`,
`tests/`), is **not** a node: the upward chain of `BOOT.md` reads through it. If the
grouping itself carries rules for everything under it, give it a pair of documents and
it becomes a node.

"Child nodes" are all descendant nodes, recursively. "Ancestors" are the nodes upward
to the root. "Neighbours" are nodes to the side (neither ancestors nor descendants).

Three clarifications:

- **A test directory is a node too.** Its `BOOT.md` describes not "tests for the
  code" but the definition of what "the node is ready" means: the levels of
  verification, what each is checked against, what is covered and what is not.
- **Code belongs to a node by the directory of its file.** Where the language has
  namespaces or packages, their path repeats the directory path from the tree root.
  This is not an architectural decision but a precondition of the checks of §13: the
  reflection check assigns a type to a node by its namespace and will attribute a type
  declared "not in its own" node to a foreign one. In a new project the rule is
  followed from the first file; in an existing one a divergence is recorded as a defect
  in the node's acceptance criteria.
- **The tree may lie inside a larger repository.** Then the nearest document above
  the tree root (`CLAUDE.md`, the repository `README.md`) plays the role of an
  **external ancestor `BOOT.md`**: it is read as a frame, inherited as a constraint and
  never edited from within the tree.

## 2. Protocol files

| File | Where it lies | Purpose | When it changes |
|---|---|---|---|
| `AGENTS.md` | tree root only | the agent's protocol (this document) | on a revision of the methodology |
| `BOOT.md` | every node | the level's context + a spec sufficient for autonomous implementation | when the node's intent changes |
| `API.md` | every node | the level's public contract (outward interfaces only) | when the node's interface changes |
| `PublicSurface.approved.txt` | tests node, optional | snapshot of the assembly's public surface; a tripwire, not a contract (§13) | together with `API.md`, in the same commit |
| loader (`CLAUDE.md` and the like) | repository root, optional | plugs in `AGENTS.md` and the root `BOOT.md`, holds the build and check commands | when the commands change |

`BOOT.md` answers the questions "what is this, why, within what frame, how to
implement it". `API.md` answers the single question "how to use this node from outside
without knowing its internals".

⚠ **The loader carries no subject-matter claims.** Anything it says about the system
becomes a second source of truth: an ancestor the tree may not edit and which
therefore goes stale first. In PastyPropellant the `CLAUDE.md` claimed that the test
project was not part of the solution when it already was; the tree could only record
for itself that the ancestor could not be trusted at that point.

## 3. Agent access model

The agent is launched in a specific directory: its **current node**. Rights are
derived from the position of the other nodes relative to the current one and differ
by **direction**.

| Direction | Nodes | What is accessible |
|---|---|---|
| **Up** | ancestors up to the root (and the external ancestor, §1) | **read** `BOOT.md` |
| **Sideways** | neighbours | **read** `API.md` |
| **Here and down** | current + descendants (recursively) | **read and write** everything (see §5: the file type depends on the mode) |

Semantics of the directions:

- **up**: the agent inherits *intent and frame*: project goals, conventions,
  constraints of the upper levels;
- **sideways**: the agent sees the neighbours' *contracts* but not their
  implementation. Reading a neighbour's code is forbidden: if its `API.md` is
  insufficient, that is a defect of the document, and it is resolved by escalation
  (§11), not by reading foreign sources. Otherwise knowledge of the neighbour's
  implementation leaks into the current node and couples the two silently;
- **here and down**: *full control* over one's own subtree.

**Priority of constraints.** The higher the node, the stronger its constraints.
Ancestors set the frame; descendants may only **narrow** it, never loosen it. On a
conflict of context the parent `BOOT.md` outranks the local one.

## 4. Working modes

Work has two stages. The mode determines the human's participation and the set of
files the agent may change.

### Design mode — human + agent, together

The goal is to build or rework the context tree. Decisions about abstractions,
contracts and boundaries are made. **Code is untouchable**: the agent touches only
`BOOT.md` and `API.md`. Scaffolding (project files, class stubs, empty modules) is
code too.

Exit of the mode: every touched `BOOT.md` has been brought to **sufficiency** (§6).

### Coding mode — agent autonomously, without a human

The goal is implementation from ready context. The agent relies on `BOOT.md` as the
exhaustive spec of the level and writes code. The right to change `BOOT.md`/`API.md`
remains: implementation may reveal a need to refine the contract, but within the
current task, not as a design session.

## 5. Rights by mode and file type

Access to foreign nodes (§3) does **not** depend on the mode. The mode restricts only
which file types the agent may change in the current node and its descendants.

| | `BOOT.md` | `API.md` | Code |
|---|---|---|---|
| **Design** | write | write | read only |
| **Coding** | write | write | write |

## 6. Transition criterion: BOOT.md sufficiency

**The boundary between the modes runs along the sufficiency of `BOOT.md`.** The
transition design → coding is legitimate only when the node's `BOOT.md` is
self-sufficient for autonomous implementation without a human.

The readiness criterion is not "we described the idea" but "an agent will write the
code from this alone, without reading the neighbours' code". A node's `BOOT.md`
counts as sufficient when it contains six sections:

- [ ] `## Purpose` — what this level is and why it exists;
- [ ] `## Invariants` — what must remain true always;
- [ ] `## Dependencies` — what the node uses from its neighbours, via links to their `API.md`;
- [ ] `## Constraints` — the frame inherited from above and set locally;
- [ ] `## Acceptance criteria` — how to tell that the implementation is correct;
- [ ] `## Taboos` — what is forbidden in this node.

**The headings are canonical and are checked by the machine letter by letter** (§13).
The language of the body is free; the headings are not translated. Additional
sections are allowed; the six mandatory ones are not optional.

**The format of `## Dependencies` is canonical too**, because the machine reads it and
compares it with the real couplings in the code:

- one link to the `API.md` of every neighbour the node uses: `[Orders](../Orders/API.md)`;
- an ancestor in whose **own** directory the used types lie: the same kind of link
  upward: `[root](../API.md)`;
- a descendant is **never** a dependency: a parent owns its children, and a downward
  link in this section is a form error;
- if the node depends on nobody inside the tree: the single word `None`;
- external packages and services: in prose after the links, starting with the line
  "Outside the tree:", with versions. The machine does not read it; a human does.

Everything else the machine reads as "no dependencies declared" and reports a
divergence from the code.

**Acceptance criteria are checkboxes, and a tick carries evidence:**

- `- [x]` carries a **date** (`YYYY-MM-DD`), the day the evidence was obtained, and
  the **place** where the evidence lies: the name of the test, fixture, measurement;
- one and the same piece of evidence is dated identically in every node that refers
  to it: diverging dates are the first sign that the criterion was rewritten rather
  than re-verified;
- a criterion dated **earlier** than the change it supposedly guards proves nothing
  and is subject to re-verification;
- a criterion with the quantifier "all" ("all fields match", "all runs pass") is
  checked against a list **generated by the machine**, not typed by hand: a typed list
  falls behind the code silently. In PastyPropellant the criterion "all fields match"
  meant fourteen fields out of twenty for three months;
- a criterion that cannot be met is not deleted but reformulated, with a note on why
  the original wording was wrong: the history of a criterion is part of the context.

## 7. Status marks in API.md

Every declaration in `API.md` is marked with a status, usually in the section heading:
`## Entry point ✅` or `## Entry point ⏳`.

- ✅ — implemented: the code block under such a heading **is checked against the
  code**: every named type and every member must exist;
- ⏳ — planned: the block is a design sketch and is not checked against the build.

The nearest mark **above** a code block decides its fate; a document without a single
mark counts as ✅ throughout. The marks are load-bearing: a ✅ on an unwritten node
turns its `API.md` into a failing check rather than a stale page. A ✅ may be placed
only after what is declared exists.

Under ✅ the signatures are **real**: types and parameter names, all overloads. A stub
like `Visit(/* parameters */)` under ✅ is a sketch passing itself off as a contract:
there is nothing to check in it, so it passes any check.

## 8. Correcting a claim

A claim in `BOOT.md` or `API.md` that turned out to be wrong **is not rewritten
silently**:

- next to the corrected text: a paragraph with ⚠: the date, what stood before, why it
  was wrong and how it was discovered. Whoever managed to act on the old wording must
  be able to find what exactly changed;
- the correction lives in the node where the wrong claim lived. The parent holds the
  current truth and a link downward: upward reading must stay cheap;
- typos and edits of form that did not change the meaning need no history.

Three kinds of claims break more often than others; check them in advance when writing:

- **absolute words** — "the only", "all", "never", "computes nothing": each must have
  proof or a caveat. The invariant "the report computes nothing" turned out wrong
  twice; "this tolerance is the only one" as soon as three more places with the same
  number were found;
- **numbers repeating the length of a list** ("seven libraries", "14 distributions"):
  they diverge from the list at its first change. Write "listed below" or keep the
  number where the machine checks it;
- **claims about a foreign node**: they go stale without the author's knowledge. Link
  to the neighbour's `API.md` instead of retelling it.

## 9. Building the tree

The direction of construction depends on the state of the project at the start.

### Greenfield — a new project, top-down

No tree yet, no code yet. Start from the root: intent, goals and invariants in the
root `BOOT.md`, the public contract of the system in the root `API.md`. Then decompose
into subdirectories, creating a pair for each, descending to the leaves. Context here
is **intent** (how it should be); it is born before the code. All declarations in
`API.md` are born under ⏳.

Decomposition goes down to the level at which the node's `BOOT.md` is sufficient (§6)
and the node no longer has to be held in the head as a whole. A directory is created
together with its pair of documents; code is not.

### Brownfield — an existing project, bottom-up

No tree yet, but there is code. Start from the leaf directories: read the code,
recover the actual contract into `API.md` and describe the level in `BOOT.md`. Going
up, at each node generalize the children's contracts into the parent's `API.md` and
synthesize the context into its `BOOT.md`. Context here is **extracted knowledge** (as
it is); the tree is reconstructed from reality. Declarations are born under ✅, and
the machine immediately checks that the recovery is right.

A recovered `BOOT.md` records the current state. Divergences from the desired state
are the subject of a separate design session (§4), not of edits along the way of the
reconstruction.

## 10. Start procedure

In any node, before doing anything, the agent:

1. reads the root `AGENTS.md` — the **protocol** (this file);
2. reads the upward chain of `BOOT.md` from the current node to the root and the
   external ancestor, if there is one — **context and frame**;
3. reads the `API.md` of the neighbours listed in the `## Dependencies` of the current
   node — **contracts**;
4. determines the mode (design / coding) and acts within its bounds;
5. runs the checks of §13 before starting the work and after finishing it. A red check
   **before** the start means the tree has already diverged from the code: first find
   out why, then work.

## 11. Escalation: changes beyond the subtree

The agent may not change nodes outside its subtree. If the task requires changing a
neighbour's or an ancestor's `API.md` (for instance, a new interface is needed from
outside), the agent **does not do it itself**. It stops and raises the change one
level up, to where both affected nodes belong to one subtree. The decision to change
the contract is made at the level that owns both.

Having stopped, the agent leaves a **proposal**, not an edit: which contract, which
change, why, which nodes are affected and what it will do once the decision is made.
The proposal lives in the task report or in the `## Acceptance criteria` of its own
node, but not in a foreign document.

This preserves encapsulation: a contract is changed only by the one in whose area of
responsibility it lies.

## 12. Declared deviations

There are nodes that cannot satisfy an article of the protocol. An example from the
first version: a node whose specification is a transcription of an external source
(fifteen hundred lines of someone else's program) cannot have a `BOOT.md` that is
sufficient "without reading foreign code": a retelling would give birth to a second
version of the model, diverging from the first.

Such a node **declares the deviation in its own `BOOT.md`**: a paragraph with ⚠
naming the article of the protocol, the reason, what replaces it and what will lift
the deviation.

The deviation is declared by **the node whose article is not satisfied**, not by its
parent and not by its child. An invariant formulated unconditionally while a violation
is known somewhere in the subtree is a hidden deviation, and the only thing worse than
it is a deviation declared not where it will be looked for.

A declared deviation is a decision for the next design session. A hidden one is a
violation of the protocol. The difference between the two is the point of this article.

## 13. Machine checks

The protocol distinguishes what the machine can hold and what no tooling can.

**Language-independent** (`protocol_lint.py`, lives in the tree as a separate node):

| Check | Article | Level |
|---|---|---|
| every node holds `BOOT.md` and `API.md` | §1 | error |
| `AGENTS.md` exists, and only at the root | §2 | error |
| every `BOOT.md` carries the six canonical sections | §6 | error |
| `## Dependencies` is in canonical form: links to `API.md` or `None`, not both | §6 | error |
| every relative link between documents resolves to a file | — | error |
| `## Dependencies` has no link to a descendant or to itself | §6 | warning |
| an `API.md` with code blocks carries a status mark | §7 | warning |
| a ticked acceptance criterion carries a date | §6 | warning |
| names declared under ✅ occur in the node's code (textual approximation) | §7 | warning |

**Require reflection and are written for the specific stack** (the reference
implementation for .NET is in the kit, `reference/dotnet/`):

| Check | Article |
|---|---|
| the assembly's public surface matches the snapshot `PublicSurface.approved.txt` | §2 |
| every exported type is named in the `API.md` of its node | §7 |
| every declaration under ✅ exists — the type and the member | §7 |
| the node's declared dependencies match the real ones — by the types in signatures **and in method bodies** | §6 |

Mistakes already paid for; whoever writes the checks should know them in advance:

- **a node is determined by the directory path** from the tree root, not by the last
  segment of the namespace: nested nodes are the norm, and a check that looks for a
  node as a direct child of the root will declare documented nodes undocumented;
- **the tree may consist of several assemblies/packages**: types are looked up in all
  of them, otherwise the declarations of the other assemblies read as nonexistent;
- **a parent using the types of its children is not a dependency**: a check demanding
  the opposite contradicts §6;
- **the tree root is looked up from the source file** (`[CallerFilePath]`,
  `__file__`), not from the binary: with a centralized build directory, climbing from
  the binary finds no tree at all;
- **links inside code blocks and in backticks are not resolved**: otherwise the
  examples of this file turn into broken links;
- **dependencies are read from method bodies too**: a static call names the type in
  no signature, and a node reaching a neighbour only that way passes the check
  undeclared.

On the surface snapshot: it is a **tripwire, not a contract**. A generated list carries
no intent; the contract is the document. The snapshot makes it impossible to change
the contract *unnoticed*: it moves in the same diff, and the reviewer sees that the
surface changed and can ask whether `API.md` moved.

**Every check must be proven non-degenerate once**: break what it guards and see it
red. A check that has never been seen red is indistinguishable from an absent one; in
the first version one such check silently let static calls through until it was made
to read method bodies.

**A perpetually red check is worse than an absent one.** If a check cannot be made
green on this tree, it is fixed or removed with a declared deviation (§12). Keeping it
red or marked "skip" is not allowed: people get used to red, and the next real failure
drowns in it.

**What the machine does not and will not check:** whether the document speaks truly
about the code it names correctly. That remains with a human and with the periodic
reconciliation of the tree with the code.

## 14. Relation to software architecture

This protocol and architectural approaches (for example, clean architecture) are
**orthogonal planes** and do not conflict.

- Architecture (layers, direction of dependencies, boundaries) consists of
  *design-level* decisions: "how the system is built". They are made above and
  recorded in the content of `BOOT.md`/`API.md`.
- The `BOOT`/`API` tree is *a navigation and access-control mechanism for the agent*:
  "how an AI works safely with an already designed system".

The tree of nodes may coincide with the architectural boundaries or be cut
differently, but it **does not dictate** the architectural decisions themselves.

---

## Appendix A. Open questions

Not part of the hard protocol, but requiring a decision at the project level:

- **Contract versioning** — how to record changes of `API.md` so that dependent nodes
  do not break. The surface snapshot (§13) makes a change visible but does not say who
  suffered from it.
- **The cost of context at depth** — parent `BOOT.md` files should be kept as short
  context headers (goals, invariants, taboos) with the details pushed down, so that
  upward reading stays cheap. There is a rule (§8) but no measure: how much edit
  history a document can carry before it stops being readable, nobody has measured.
- **Reflection checks outside .NET** — the four checks of §13 require reading the
  assembly. For Python/TypeScript/Go they must be written anew; the
  language-independent linter covers only the file half.

## Appendix B. What version 3 added

Everything listed was lived through on the second tree and only then written down:

- §1 — what counts as a source-code directory; grouping directories are transparent;
  code belongs to a node by directory, the namespace repeats the path;
- §2 — the loader (`CLAUDE.md`) carries no subject-matter claims;
- §3 — reading a neighbour's code is forbidden explicitly, not by implication;
- §6 — ancestor and descendant in `## Dependencies`; external dependencies in prose;
  the dating rules for criteria and the criterion with the quantifier "all";
- §7 — real signatures under ✅, without stubs;
- §8 — a new article: how a wrong claim is corrected and which claims break more
  often than others;
- §11 — escalation leaves a proposal, not an edit;
- §12 — a deviation is declared by the node whose article is violated;
- §13 — a linter instead of a skill, the extended list of checks, six already
  paid-for mistakes in the reflection checks, the ban on a perpetually red check.
