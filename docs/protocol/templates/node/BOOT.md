# BOOT.md — <Node>

<!-- The six sections below are mandatory, the headings are not translated and are
     checked by the linter letter by letter (AGENTS.md §6). The language of the body is
     free. Additional sections are allowed. There is one measure of readiness: an agent
     will write the code from this document alone, without reading the neighbours'
     code. Whatever is missing for that is a hole, not brevity. -->

## Purpose

<What this level is and why it exists: which problem it solves and why it is a
separate node rather than part of a neighbour. Two or three sentences.>

## Invariants

- <What must remain true always. A checkable wording: not "the code is clean" but
  "the solver holds no calculation state: it lives in the context passed in".>
- <An invariant that has already been violated once is worth more than an invented
  one: if something broke in the past, the invariant must forbid exactly that.>

## Dependencies

- [<Neighbour>](../<Neighbour>/API.md) — <what exactly the node takes from it>.

Outside the tree: <packages, services, data files, with versions>.

<!-- If there are no dependencies inside the tree, one line instead of the list: None
     A descendant is never a dependency: a parent owns its children (§6). -->

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

<!-- The link leads to the nearest node ABOVE, and it may not be in the adjacent
     folder: grouping directories (src/, tests/) are not nodes and are skipped, so from
     src/Orders the path to the root is ../../BOOT.md. The linter checks that the link
     leads to an existing file but does not guess whether it is the right node. -->


- <local frame: hot path, platform, data format, no allocations…>

## Acceptance criteria

- [ ] <A checkable claim: what confirms it and against what it is compared.>
- [ ] <A criterion with the quantifier "all" is checked against a machine-generated list.>

<!-- When ticking [x], add the date (YYYY-MM-DD, the day the evidence was obtained) and
     the name of the test/fixture/measurement. An unattainable criterion is reworded
     with an explanation, not deleted. -->

## Taboos

- <What is forbidden in this node, and why, in one sentence. A taboo without a reason
  is not respected.>
