# Prompt 5. The checks that compare the documents with the code

**When:** the tree exists (created or reconstructed), the stack is chosen, code has appeared.
**Mode:** coding: the code of the checks is written in the tests node.
**Output:** four reflection checks, each proven non-degenerate and included in the
project's ordinary test set.

Without these checks the tree will diverge from the code: not "may diverge" but will.
The language-independent linter holds only the form of the documents.

---

Task: write the half of the protocol's machine checks that compares the documents with
the code (`AGENTS.md`, §13). Stack: `<language, runtime, test framework>`. Place:
`<the tests node where they will live>`.

Read `AGENTS.md` in full, then §13 once more: there is the list of mistakes already
paid for; none of them needs to be repeated.

**The four checks:**

1. **The public surface matches the snapshot.** A description of the whole exported
   surface of the assembly/package is generated (types, members, signatures,
   distinguishing `init`/`set` and the like) and compared with the recorded file
   (`PublicSurface.approved.txt`). On a divergence: write `.actual.txt` next to it and
   demand that the snapshot be updated **in the same commit as `API.md`**. The
   snapshot is a tripwire, not a contract: it carries no intent, it only makes it
   impossible to change the contract unnoticed. A missing snapshot is not a reason to
   pass: write it and fail once with a clear message.
2. **Every exported type is named in the `API.md` of its node.** The snapshot does not
   catch this: it is generated from the same code. A node is determined by the
   **directory path** from the tree root.
3. **Every declaration under ✅ exists**: both the type and the member. Strictness is
   tied to the marks: a block under ⏳ is not checked, a document without marks counts
   as ✅ throughout. Names, not full signatures: the signatures are already pinned by
   the snapshot, and a second copy of them in prose is a second thing to keep in agreement.
4. **The declared dependencies match the real ones.** The real ones are read from the
   signatures **and from the method bodies** (bytecode, AST, imports: whatever the
   language gives): a static call names the type in no signature. Check in both
   directions: an undeclared dependency and a declared nonexistent one. A parent using
   the types of its children is not a dependency; an ancestor whose own types a child
   uses is.

**Mandatory conditions:**

- **Every check is shown red once.** Break exactly what it guards (rename a member in
  `API.md`; remove a dependency line; add a public type and do not describe it; put a
  ✅ on something nonexistent), make sure it fails and that the message contains the
  address: the file, the node, the name. List the mutations in the report and what
  each gave. A check that has not been seen red is indistinguishable from an absent one.
- **A false positive is not acceptable.** Run the checks over the whole tree and
  explain **every** finding. If a check is red because of its own construction and not
  because of the quality of the documents, fix the check, not the documents. A
  perpetually red check is either fixed or removed with a declared deviation (§12);
  keeping it red or marked "skip" is not allowed.
- **The tree root is looked up from the source file** (`[CallerFilePath]`, `__file__`,
  the equivalent), not from the binary: with a centralized build directory, climbing
  from the binary finds no tree at all.
- **Links inside code blocks and backticks are not resolved**, otherwise the examples
  of `AGENTS.md` turn into broken links.
- The checks go into the **ordinary** test command: a check that has to be run
  separately is not run.
- The tests node describes them in its `BOOT.md`/`API.md`: what exactly is now
  guaranteed to the tree, and what these checks do not guarantee.

**Reference:** the kit contains a working implementation for .NET (`reference/dotnet/`):
`ProtocolTests` (a pair of documents in every node, the surface snapshot) and
`DocumentationTests` (dependencies from bytecode, coverage of types, declarations under
✅, sections, links). It is a reference, not a file ready to paste: read
`reference/dotnet/README.md`; it says what in it is particular and what has to be
changed. If the stack is different, the reference is useful as a list of solved
problems: walking the declarations of a document, determining the status of a block
by the nearest mark, reading couplings from method bodies.

**Report:** which checks were written and where they live; by which mutation each was
shown red; how many findings the first run over the tree gave and what was done with
each; what of §13 could not be implemented and why (with a declared deviation).
