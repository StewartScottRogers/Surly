# Decisions

Architecture decision records for the Surly monorepo. One file per decision, numbered `ADR-0001-...md` onward, in the same form the Curl and Surl repositories use.

## Recorded

- [ADR-0001](ADR-0001-licensing-per-folder.md) - Licensing is decided per folder; the book and its code are all rights reserved.
- [ADR-0002](ADR-0002-the-imprint-is-oh-really-media.md) - The imprint is "Oh Really Media", and the book's identity lives in one file.
- [ADR-0003](ADR-0003-attribution-first-rights-and-the-licence-split.md) - Attribution first; prose under Creative Commons Attribution-NoDerivatives, example code under Apache 2.0. Checklist in `Book/RIGHTS.md`.

## Open

- Migration method for the two solutions (subtree with history, or otherwise) and when.
- Shared build properties and quality gates at the root versus per solution.
- Release and versioning of the two programs from one repository.
- Chapter and recipe layout of the book, and how recipes reference code and commands in the two solutions.
- Whether the dark factory works in this repository, and from which folder.
