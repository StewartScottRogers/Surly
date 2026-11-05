# ADR-0001: Licensing is decided per folder; the book and its code are all rights reserved

## Status

Accepted, 2026-11-05.

## Context

Surly holds three things with different owners' intentions: the Curl port, released under the curl licence; Surl, released under the MIT licence; and the Surly cookbook with its example code, which Stewart Rogers writes and owns outright. The two programs are and will stay open source. The book and every line of example code written for it are not open source: they are real, extensive, and the author's sole property.

## Decision

1. `Curl/` carries the curl licence (SPDX `curl`), as the Curl repository does today. Its `LICENSE.txt` comes across with the migration and governs everything under that folder.
2. `Surl/` carries the MIT licence, as the Surl repository does today. Its `LICENSE.txt` comes across with the migration and governs everything under that folder.
3. `Book/` is copyright © 2026 Stewart Rogers, all rights reserved. No licence of any kind is granted. `Book/LICENSE.txt` states this, and every source file written for the book carries a copyright header saying the same. This covers the text, the recipes, and all example code, scripts, configuration and data created for the book, wherever in `Book/` they sit.
4. The root `LICENSE.md` is a pointer, not a licence: it names the three folders and the terms each one is under, and states that nothing outside `Curl/` and `Surl/` is licensed.
5. Example code for the book never lives under `Curl/` or `Surl/`, so it never falls under their open-source terms. If a recipe needs a change to either program, the change is made in that program under that program's licence, and the recipe that uses it stays in `Book/`.
6. No contribution to `Book/` is accepted from anyone but the author, so ownership never becomes shared. Contributions to `Curl/` and `Surl/` follow those projects' own rules.

## Consequences

- The repository is public, so the book's source is readable by anyone, and GitHub's terms let other users view and fork a public repository. All rights reserved stops them from using, copying, modifying or redistributing it beyond that, but it does not stop them reading it. If that is not acceptable, the book moves to a private repository and Surly references it; that is a separate decision.
- A reader of a recipe may run the `surl` and `curl` commands it shows, since those programs are open source; the recipe text and any code written for the book remain the author's.
- Nobody involved in writing this record is a lawyer. Before publication, the licence text and headers should be reviewed by one, and the publisher's contract will govern the book's text from that point.
