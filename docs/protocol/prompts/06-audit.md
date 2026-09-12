# Prompt 6. Reconciling the tree with the code (audit)

**When:** periodically: after several coding sessions, before a release, after a
large branch is merged.
**Mode:** design (code is only read).
**Output:** corrected claims with a trace of the correction, and a list of divergences.

This is the half of the work the machine does not take: **whether the document speaks
truly about the code it names correctly.** By this point the linter and the reflection
checks are green; the audit starts where they end.

---

An audit of the subtree `<path; or "the whole tree">`. The mode is design: code is
read, not edited. Read `AGENTS.md`, especially §8 and §13.

For every node of the subtree, starting from the leaves:

1. **Invariants.** For each, find in the code the place that makes it true, and name
   it. An invariant for which no place was found is either wrong or held by
   convention: in the first case correct it with a ⚠ trace; in the second, add what
   holds it and what happens if the convention is broken.
2. **Absolute words.** Find "the only", "all", "never", "always", "nothing …" and check
   each with a search over the tree. They are the first to break: the number of places
   with one and the same constant grows, and "the only place" stops being the only one.
3. **Numbers.** Every number in prose repeating the length of a list or the count of
   something in the code ("seven modules", "four checks", "39 tests"): recount it. A
   divergence is not a typo but a sign that the list changed without the document.
4. **Contracts.** `API.md` against the actual surface: not only names but also the
   signatures under ✅ (parameters, overloads, optionality). A stub under ✅ is a
   separate finding: it passes any check.
5. **Acceptance criteria.** For every `- [x]`: does the evidence exist and is it named
   correctly? Is the date not earlier than the change the criterion guards (compare
   with `git log`)? Is a criterion with the quantifier "all" really about all of them,
   not about a part of the list?
6. **Hidden deviations.** An invariant formulated unconditionally while a violation is
   alive somewhere in the subtree is a hidden deviation (§12). Check the invariants of
   the upper nodes against the code of the lower ones: that is exactly where these hide.
7. **Dependencies and frame.** Has the node acquired a coupling that is not in
   `## Dependencies` and that the check does not see (data, files, environment
   variables, queues: everything that is not a type)? Has a descendant loosened an
   ancestor's frame?

**How to correct:** only documents, only within the bounds of the audited subtree, and
every correction of a claim with a trace (§8): ⚠, the date, what stood before, why it
was wrong. Do not correct claims about foreign nodes: leave a proposal (§11).

**Report:** how many claims were checked and how many turned out wrong (this is the
measure of the audit's usefulness); every corrected one in a single line "was / is /
why"; the divergences "as is / as it should be" found that must be resolved by code
rather than by a document; hidden deviations; proposals for foreign nodes; what the
checks of §13 would not have caught, and whether a new check is worth adding.
