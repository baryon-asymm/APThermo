# Prompt 2. Bringing an existing project under the concept (brownfield)

**When:** there is code, there is no document tree.
**Mode:** design (`AGENTS.md`, §4): **not a single line of code is edited**.
**Output:** the tree is reconstructed bottom-up, the linter is green, the divergences
"as is / as it should be" are recorded, not fixed.

The work is done in **slices**: one subdirectory per session. Reading the code is the
most expensive part, and an attempt to cover the whole repository at once yields
documents that retell file names. Below is the text of the prompt; it is meant to be
run repeatedly until the slices run out.

---

We are bringing an existing project under the document-tree protocol. Read `AGENTS.md`
at the tree root in full: it is the protocol, and in everything concerning the tree
it outranks this message.

The mode is **design** (§4). **Code is untouchable.** Do not rename, do not refactor,
do not "fix" what you find, do not add or delete code files, do not edit manifests.
Everything you found and want to fix goes into the documents as a recorded
divergence, and is resolved later in a separate session. A recovered `BOOT.md` records
the **current** state, not the desired one (§9).

**Input:**

- the tree root: `<path; usually the repository root>`;
- the slice of this session: `<path to a subdirectory, or "inventory" if this is the first session>`;
- the stack: `<language, version, build, test framework>`;
- what to exclude from the tree: `<generated code, vendored dependencies, legacy directories>`.

## Session 1 — inventory (only if there is no tree yet)

1. Run `python tools/protocol-lint/protocol_lint.py <root> --list-nodes`. This is
   the list of directories the protocol considers nodes. Compare it with reality:
   what here is junk (generated, vendored, dead), what should be excluded with the
   `--exclude` flag, and what are real levels of abstraction.
2. Give me a **list of nodes for approval**: path, one line of purpose, who depends on
   whom in the code (not by intent, by actual imports and calls), and a mark "leaf /
   intermediate / root / tests". Mark separately:
   - directories where the namespace or package **does not match** the directory
     path (§1): this is a defect to be recorded, not fixed;
   - cycles between directories;
   - directories that do not amount to a level of abstraction (a dump of utilities):
     their boundaries I will have to decide.
3. Propose the order of slices: bottom-up, leaves first, and in each slice the nodes
   that can be described without looking into the neighbours.
4. Create in the root `BOOT.md` (or, if it does not exist yet, in a draft that I will
   approve) a temporary section `## Reconstruction`: a checklist of nodes with their
   status. It will be the memory between sessions; when the tree is reconstructed, the
   section is removed.

Write nothing in the first session except this list and the checklist section.

## Session N — one slice

For every node of the slice, **bottom-up**:

1. **Read the node's code in full.** Not by file names and not by type names; the
   bodies too: a dependency through a static call is visible in no signature, and the
   document must declare it.
2. **Read the history.** `git log --format='%h %ad %s' --date=short -- <path>` and the
   diffs of the pivotal commits. Reverted attempts, reasons for edits and messages like
   "put it back as it was" are the main source of **taboos** and invariants: this
   knowledge cannot be extracted from the code, and the tree is where it belongs.
3. **Write `API.md`**: the actual outward contract, under ✅ (§7): only what is really
   exported, with real signatures. Improve nothing along the way: if something
   superfluous sticks out, that is a record among the divergences, not a reason to
   hide it. Add the sections "errors", "side effects", "what the node does not do";
   the last one saves the neighbours from guessing.
4. **Write `BOOT.md`**: six sections (§6), and every claim must be **extracted, not
   invented**:
   - `## Invariants`: only what the code really observes, and next to it what holds
     it (a test, the shape of the code, review). A property that "seems to be
     observed" is written as `- [ ]` in the acceptance criteria, not as an invariant;
   - `## Dependencies`: from the actual couplings, in canonical form; an ancestor is
     allowed, a descendant is not;
   - `## Constraints`: inherited from above plus local ones (hot path, platform, data format);
   - `## Acceptance criteria`: what is **already** checked (`- [x]` with a date and a
     test name; take the date from the history, not from today) and what is not
     checked (`- [ ]`). The uncovered part is half the value of this document;
   - `## Taboos`: what is forbidden here and why. Each taboo preferably with a price:
     what breaks if it is violated.
5. **Record the divergences, do not fix them.** Dead code, a promise the code does not
   keep, a hard-wired constant, a directory with a foreign namespace, a cycle between
   nodes, a copy of data instead of a reference: all of it goes into
   `## Acceptance criteria` as a `- [ ]` item with a ⚠ mark and one sentence on why it
   is bad.
6. **If an article of the protocol cannot be satisfied for the node, declare a
   deviation** in its own `BOOT.md` (§12): the article, the reason, what replaces it,
   what will lift the deviation. A hidden deviation is a violation; a declared one is
   a task for the future.
7. **Having gone one level up:** the parent's `API.md` generalizes the children's
   contracts (how the system is used + the list of children), the parent's `BOOT.md`
   holds the common invariants and frame, and nothing that is already said in the children.

At the end of the slice: update the `## Reconstruction` checklist, run the linter
(`python tools/protocol-lint/protocol_lint.py <root>`) and get to zero errors.

**Report:** which nodes were described; which invariants could be extracted and what
holds each of them; what was recorded as divergences (as a list: this is the work
plan); which deviations were declared; what is left for the next slice; which
decisions are needed from me.

What not to do: do not edit code; do not improve contracts along the way; do not write
an invariant the code does not observe; do not put `- [x]` on what is not checked
automatically or has not been checked by hand with a date; do not retell the
neighbours instead of linking to their `API.md`; do not remove inconvenient truth from
the documents: in brownfield it is the result.

## After the tree is reconstructed

1. The reflection checks for your stack: prompt `05-machine-checks.md`. It is they
   that hold what was reconstructed: without them the tree diverges from the code at
   the first edit.
2. The list of divergences is the backlog. Each is cured either by a design session
   (prompt `03-design-node.md`) or by coding (prompt `04-coding-node.md`), and both
   already rely on the tree rather than on reading the whole repository.
3. Periodic reconciliation of the documents with the code: prompt `06-audit.md`.
