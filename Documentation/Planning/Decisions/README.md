# Decisions

Architecture decision records for the Surly monorepo. One file per decision, numbered `ADR-0001-...md` onward, in the same form the Curl and Surl repositories use.

## Recorded

- [ADR-0001](ADR-0001-licensing-per-folder.md) - Licensing is decided per folder; the book and its code are all rights reserved.
- [ADR-0002](ADR-0002-the-imprint-is-oh-really-media.md) - The imprint is "Oh Really Media", and the book's identity lives in one file.
- [ADR-0003](ADR-0003-attribution-first-rights-and-the-licence-split.md) - Attribution first; prose under Creative Commons Attribution-NoDerivatives, example code under Apache 2.0. Checklist in `Book/RIGHTS.md`.
- [ADR-0004](ADR-0004-asciidoc-source-and-a-structure-that-grows.md) - One AsciiDoc source, recipes keyed not numbered, ordered by difficulty, indexed for machines.
- [ADR-0005](ADR-0005-the-recipe-is-an-executable-specification.md) - A recipe is an executable specification; the prose is a view of it. Lint, verify, review gates.
- [ADR-0006](ADR-0006-a-dark-factory-for-the-book-that-never-blocks.md) - A dark factory for the book that never blocks, feeds the program boards, and is built as policy on a runtime-neutral harness. Lives in `Factory/`.

## Open

The book's method is settled (ADR-0004, ADR-0005, `Book/AI.md`). Still to decide:

- Migration method for the two solutions (subtree with history, or otherwise) and when.
- Shared build properties and quality gates at the root versus per solution.
- Release and versioning of the two programs from one repository.
- The first chapter list within the difficulty tiers.
- When the Curl and Surl boards migrate onto `Factory/` (with the programs' migration).
