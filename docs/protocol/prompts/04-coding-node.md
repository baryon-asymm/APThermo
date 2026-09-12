# Prompt 4. Autonomous coding on a node

**When:** the node's `BOOT.md` is sufficient (§6), the contract in `API.md` is described.
**Mode:** coding (`AGENTS.md`, §4): the agent works alone, without a human.
**Output:** the implementation, checks by the acceptance criteria, `[x]` ticks with
dates, ✅ marks on what really exists.

---

Coding on the node `<path to the node>`. Work autonomously: ask no questions, make the
decisions yourself within the bounds set by the documents, and write everything you
had to decide beyond those bounds into the report.

**Start** (`AGENTS.md`, §10): the protocol; the upward chain of `BOOT.md` to the root;
the `API.md` of the neighbours from this node's `## Dependencies`; the linter. **Do
not read the neighbours' code** (§3): their contract is their `API.md`. If the contract
is insufficient, that is a defect of their document: stop and escalate (§11), do not
peek into the implementation.

A red linter **before** the start of the work means the tree has already diverged
from the code. Then do not work: find out why, and report.

Task: `<what to implement; or "implement the node by BOOT.md in full">`.

Rules:

1. **`BOOT.md` is the spec.** Invariants are mandatory, taboos are prohibitive,
   constraints may be narrowed but not loosened. Do not invent beyond the contract:
   extra public surface is a contract nobody asked for, and it will have to be
   described and maintained.
2. **If the document is insufficient for a local decision**, make a reversible
   decision within the node's bounds and record it: in `BOOT.md` as a line with ⚠
   ("assumption made: …, because …") or as a `- [ ]` criterion. An irreversible
   decision, or one touching the neighbours, is a reason to stop, not to guess.
3. **Checks are written from the acceptance criteria**, not fitted to the
   implementation. Every check must be shown red once: break what it guards and make
   sure; say in the report how exactly (§13).
4. **Marks and ticks only by fact.** ⏳ → ✅ is switched when what is declared exists,
   with real signatures. `- [ ]` → `- [x]` with a date and the name of the test or
   fixture. The document and the code move **in one commit**.
5. **The contract may be refined, but not quietly.** If implementation reveals that
   the node's `API.md` is wrong, edit it and say so in the report as a separate line:
   "the contract changed thus, because …". Changing a foreign contract is an escalation.
6. **A deviation from the protocol is declared** in this node's `BOOT.md` (§12) if an
   article cannot be satisfied here: the article, the reason, the replacement, what
   will lift it.
7. **Finish:** the linter without errors; the reflection checks, if the project has
   them; the project's test set green. If something is red, say so, with the output,
   not "works on the whole".

**Report:** what was implemented; which acceptance criteria were closed and by what
exactly; which marks were switched to ✅; which assumptions were made and why; what
remains open; escalations (neighbours' contracts); declared deviations; the result of
the linter and the tests, in numbers.
