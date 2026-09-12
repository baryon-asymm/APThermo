# API.md — <Node>

<Namespace / package: `<Name>`.> <In one sentence: what the node exposes outward.>
Everything not listed here is internal structure and may change.

<!-- Status marks (AGENTS.md §7): ✅ — implemented, the code block is checked against
     the code; ⏳ — planned, the block is a sketch. The fate of a block is decided by
     the nearest mark ABOVE it; a document without marks reads as ✅ throughout. Under
     ✅ the signatures are real, without stubs like (/* parameters */). -->

## <Contract> ⏳

```<language>
<declarations: types, members, signatures — those the node is used through from outside>
```

<Semantics: preconditions and postconditions, units, ranges, how a call can end,
thread safety.>

## Errors

| Situation | Behaviour |
|---|---|
| <invalid input> | <what is thrown/returned and when: before the work starts or along the way> |

## Side effects

<Files, network, global state, writes into the working directory — or "none".>

## Out of scope

- <What not to expect from the node and whose responsibility it is. The section saves
  the neighbours from guessing and the owner from needless questions.>
