# Prompt 3. A design session on one node

**When:** the node exists in the tree, but its `BOOT.md` is not yet sufficient for
autonomous coding, or the node's intent is changing.
**Mode:** design (`AGENTS.md`, §4): code is only read.
**Output:** the node's `BOOT.md` is sufficient (§6), or is explicitly named
insufficient with a list of what is missing and on whom it depends.

---

A design session on the node `<path to the node>`.

Carry out the start procedure (`AGENTS.md`, §10): the protocol, the upward chain of
`BOOT.md` to the root, the `API.md` of the neighbours from this node's
`## Dependencies` section, then the linter. The mode is **design**: code is not
edited, scaffolding is not created.

The task of the session: `<what is wanted from the node: bring to sufficiency / revise
the contract / split into child nodes / close a divergence from the list>`.

Order:

1. **Say what is already there and what is missing.** Go through the six sections and
   for each answer: sufficient / insufficient, and if insufficient, which question
   exactly remains unanswered.
2. **Sufficiency check by role play.** Take the role of the agent who will write this
   node from scratch without reading the neighbours' code and without asking
   questions. Write out the list of decisions it will have to make on its own. Each
   decision is either an answer in `BOOT.md` or a question to me. Show me this list:
   it is the measure of sufficiency.
3. **Edit the documents.** Invariants as checkable wordings; dependencies in canonical
   form; acceptance criteria such that a check can be written from them, not a report.
   The contract in `API.md`: what does not exist yet under ⏳; what exists under ✅
   with real signatures.
4. **When correcting a claim, leave a trace** (§8): ⚠, the date, what stood before,
   why it was wrong. This especially concerns absolute words ("the only", "all",
   "never") and numbers repeating the length of a list.
5. **Is a split into child nodes needed?** If the node cannot be described without
   "and it also does this", propose a decomposition: directories, the purpose of each
   part, boundaries, direction of dependencies. Create the child pairs only after my agreement.
6. **Do not cross the subtree boundary** (§11). If a neighbour's or an ancestor's
   `API.md` needs to change, stop and leave a **proposal**: which contract, which
   change, why, which nodes are affected, what you will do after the decision. Do not
   edit a foreign document.
7. **The linter before and after.** Errors down to zero; warnings explained.

**Report:** whether `BOOT.md` is sufficient now (yes / no, and what is missing); what
changed in the contracts and who will feel it; which claims were corrected and why
they were wrong; proposals for foreign nodes; whether the node can be handed over to
coding (prompt `04-coding-node.md`).
