# BOOT.md — <Project> (tree root)

<!-- The root BOOT.md is read by every agent in every session (AGENTS.md §10), so it is
     short: goals, invariants, frame, taboos. Details live in the child nodes. -->

## Purpose

<What we build and why, for whom, what comes in and what comes out. One or two paragraphs.>

Not goals: <what the project deliberately does not do, so that the agent does not build extras>.

## Invariants

- <A property without which the system is useless. Checkable.>
- <…>

## Dependencies

None.

Outside the tree: <language and version, platform, key libraries and services, with versions>.

## Constraints

- <Platform, language, environment where this runs.>
- <Performance, data volumes, memory.>
- <Security, licences, personal data.>
- <Repository rules: branches, commits, what may not be published.>

<There is no external ancestor: the tree root coincides with the repository root, and
the loader (`CLAUDE.md`) carries no subject-matter claims (AGENTS.md §2).>

## Acceptance criteria

- [ ] <How to tell the system is ready: what checks it and against what.>
- [ ] The tree passes `protocol_lint` without errors.
- [ ] The reflection checks are written for this stack and each is proven
      non-degenerate (AGENTS.md §13).

## Taboos

- <What is forbidden across the whole project, and why.>

## Decomposition

<Why the tree is cut this way: the node boundaries and the direction of dependencies
between them. The list of nodes itself is in `API.md`, section `## Children`; here
only the rationale, so that the list and its reason do not diverge.>
