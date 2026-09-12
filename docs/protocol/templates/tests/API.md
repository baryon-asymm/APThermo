# API.md — <Node>.Tests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the parent may consider proven.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| <what exactly counts as proven> | <level, test names, what it was checked against> | ⏳ |

Claims do not scale up the ladder: a green upper level with a red lower one means not
"the node is correct" but "matched for an unknown reason".

## What the tests rely on

<The internal machinery of the tests themselves that they rely on: the reference
loader, path lookup from the repository root, the tolerance table. It is described
here because tests are code too, and their own supports must be a contract as well.>
