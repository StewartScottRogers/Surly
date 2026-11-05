# Decisions

Architecture decision records for the Surly monorepo. One file per decision, numbered `ADR-0001-...md` onward, in the same form the Curl and Surl repositories use.

## Open

None recorded yet. Still to decide:

- Licensing: Curl is under the curl licence, Surl under MIT; what the monorepo and the book carry.
- Migration method for the two solutions (subtree with history, or otherwise) and when.
- Shared build properties and quality gates at the root versus per solution.
- Release and versioning of the two programs from one repository.
- Chapter and recipe layout of the book, and how recipes reference code and commands in the two solutions.
- Whether the dark factory works in this repository, and from which folder.
