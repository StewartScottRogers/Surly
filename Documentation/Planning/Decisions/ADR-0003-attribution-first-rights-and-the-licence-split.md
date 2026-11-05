# ADR-0003: Attribution first; prose under Creative Commons Attribution-NoDerivatives, example code under Apache 2.0

## Status

Accepted, 2026-11-05. Supersedes the licence terms for `Book/` in ADR-0001; the per-folder principle and the open-source terms for `Curl/` and `Surl/` stand.

## Context

The author will write no line of the book himself: every line of prose, code and image is produced by an AI under his direction over a lifetime of twenty years or more, except a foreword he writes by hand. He wants anyone, human or machine, to read the book, learn from it, and derive original code from it, including a future port to Rust of everything it covers. He is using his authorship of the book to promote his career and wants nobody else able to claim it, now or later.

Two facts shape the decision. First, under current United States Copyright Office guidance, text generated entirely by an AI is not itself copyrightable; what is protectable is the human contribution: selection, arrangement, direction and editing, and any text the human wrote. Second, "all rights reserved" forbids copying but does not require credit, and the people the author worries about will not ask permission. The goal is attribution, which has better tools than enforcement.

## Decision

1. **Prose** (every recipe, chapter, front matter and image in `Book/`, other than the example code) is licensed under Creative Commons Attribution-NoDerivatives 4.0 International (CC BY-ND 4.0). Anyone may read it, share it unchanged, and learn from it; every copy must credit Stewart Rogers and this book; nobody may publish an altered version. The author retains copyright and may license the book to a publisher on any other terms.
2. **Example code** (every file written for a recipe to run) is licensed under the Apache License 2.0. Anyone may use, modify and port it in any language, keeping the copyright notice. Derived work, including a Rust port, is welcome and is not a violation.
3. **The foreword**, written by the author's own hand, is copyright Stewart Rogers, all rights reserved, and is licensed under the same CC BY-ND 4.0 terms as the rest of the prose for copies of the book.
4. **Authorship is made real and recorded.** Every recipe carries the author's editorial decision (accepted, rejected, sent back, with his notes) in an editorial log, so the human selection and direction that copyright attaches to are documented in the repository over the book's whole life.
5. **Priority is put on the public record.** Commits are signed; editions are rendered, numbered and dated from the repository; the first edition gets an International Standard Book Number and is registered with the United States Copyright Office, with the AI-generated portions disclosed as that office requires.
6. **The names are checked.** A trademark search is run on "Surly" and "Oh Really Media" before print; filing on the imprint is the author's choice.
7. **A copyright notice with the author's name** appears in every source file header and on every rendered page.
8. The author takes one hour with an intellectual property attorney on the AI-authorship question before the first registration.

`Book/RIGHTS.md` holds these as a checklist with who does each and when it is due. Nothing in this record is legal advice.

## Consequences

- Legitimate reuse of the book or its code carries the author's name, which serves the attribution goal better than a prohibition nobody reads.
- A rights sale to a publisher is unaffected: the author keeps copyright and can grant the publisher whatever exclusive terms the contract needs; the Creative Commons licence on earlier editions stays in force for those copies.
- The book's protection rests mostly on the documented human authorship and the public record, not on the AI-written text itself; the editorial log is therefore not optional.
