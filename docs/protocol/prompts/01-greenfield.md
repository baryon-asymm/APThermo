# Prompt 1. A new project from scratch (greenfield)

**When:** the repository is empty or nearly empty; there is no code yet.
**Mode:** design (`AGENTS.md`, §4): code and scaffolding are not created at all.
**Input:** `AGENTS.md`, `CLAUDE.md` and `tools/protocol-lint/` lie at the root.
**Output of one session:** the root pair of documents + the first-level nodes with
their pairs, the linter green, a queue of nodes to bring to sufficiency.

Fill in the `<…>` and hand the text below to the agent.

---

We are starting a new project under the document-tree protocol. Read `AGENTS.md` at
the repository root in full: it is the protocol, and in everything concerning work
with the tree it outranks this message. If anything here contradicts it, it is right,
and tell me so.

The mode is **design** (§4). You write only `BOOT.md` and `API.md`. Code, project
files, package manifests, module and class stubs: **none of these are created**, not
even empty. The only exception is directories: a node is born as a directory with a
pair of documents.

**Project intent:**

<One to three paragraphs in your own words: what we build, for whom, what comes in and
what comes out, how it differs from ready-made solutions, what is already decided for
good (language, platform, hosting) and what is open.>

Order of work:

**1. Questions, in one batch.** Ask everything without which the root `BOOT.md` will
not become sufficient (§6): purpose and **non-goals**; who uses this and how; the data
and their sources; invariants whose violation makes the system useless; constraints
(platform, language, performance, volumes, licences, personal data); repository rules
(branches, commits, what may not be published); how we will know the system is ready.
Do not ask about things that have a reasonable default: propose the default and mark
it as a proposal. Wait for the answers; do not start writing.

**2. The root pair.** From the answers write the root `BOOT.md` and `API.md` (the
templates are in `docs/protocol/templates/root/`, if they are in the repository;
otherwise the structure is given by §6 and §7). Requirements:

- the root `BOOT.md` is short: every agent reads it in every session. Goals,
  invariants, frame, taboos, and nothing that can be said lower down;
- in `## Invariants`, only checkable wordings. "The code is clean" is not an
  invariant; "no answer to the user is built from unvalidated input" is;
- in `## Acceptance criteria`, that by which the readiness of the system can be
  **checked**; every item `- [ ]`, there can be no ticks yet;
- all declarations in `API.md` under ⏳ (§7): there is no code, a ✅ would be a lie.

**3. Decomposition.** Propose the first-level nodes: for each, the directory name, one
line of purpose, what it exposes outward and whom it depends on. Show me the direction
of the dependencies and explain why the boundary is drawn there. Wait for my
agreement: redrawing boundaries costs more than anything else.

Cutting rules: a node is one level of abstraction and one responsibility; a cycle
between nodes is a sign of a wrong boundary, not a normal coupling; keep the depth
such that the chain of `BOOT.md` from a leaf to the root stays cheap to read.

**4. Pairs for the agreed nodes.** Each gets a directory, a `BOOT.md` with all six
sections and an `API.md` with a sketch of the contract under ⏳. The `## Dependencies`
section in canonical form (§6): links to the neighbours' `API.md`, or `None`. Where a
node has not yet been brought to sufficiency, write in its `BOOT.md` an explicit list
of what is missing, in `## Acceptance criteria` or in a separate `## Open questions`
section. Silent incompleteness is worse than a declared one: code will be written from it.

**5. Check.** Run `python tools/protocol-lint/protocol_lint.py .`. There must be no
errors; explain or remove the warnings. Note: the linter treats any directory with
code, a manifest or a document as a node. If it sees nodes you did not create, there
is code in the repository and this is already brownfield (prompt 2).

**Report at the end of the session:** which nodes were created; which of them are
already sufficient for autonomous coding and which are not, and what they lack; which
decisions I made in my answers and which you made by default; open questions; the
queue of nodes to bring to sufficiency (prompt 3), in the order they should be taken.

What not to do: do not create code or scaffolding; do not create nodes whose
boundaries have not been agreed with me; do not put ✅; do not invent invariants that
sound nice and forbid nothing; do not transfer the content of this message into the
tree verbatim: it is a draft, and the tree is written clean.
