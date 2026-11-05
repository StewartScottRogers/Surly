# ADR-0002: The imprint is "Oh Really Media", and the book's identity lives in one file

## Status

Accepted, 2026-11-05.

## Context

The book is written to the standard and in the form of O'Reilly Media's cookbooks, and the author wants it to be easy to sell the rights to a publisher who may rename it. The author cannot use O'Reilly's name. A self-published imprint name is needed for the cover, the copyright page and the metadata, and it must be one a proofreader cannot accidentally turn into "O'Reilly".

Sound-alike and spelling-alike names (O'Really, O'Riley, O'Rielly) were considered and rejected: they fail the likelihood-of-confusion test, they are one or two characters from the original so a typo can slip through proofreading, and pitching a manuscript dressed as the buyer's own brand starts a rights conversation badly.

## Decision

1. The imprint is **Oh Really Media**. Three ordinary English words, no apostrophe, no O' prefix: nothing in its shape can drift into "O'Reilly" without the change being visible.
2. The imprint name, the title, the subtitle, the author and every other piece of the book's identity are set once in `Book/book.yml` and nowhere else. Every page, header, cover and build script reads them from that file. A publisher renames the book or replaces the imprint by editing one field.
3. Nothing in the book's files carries O'Reilly Media's name, series names, animal engravings, typography or other trade dress. The book copies their conventions (the Problem / Solution / Discussion form, the front-matter order, the code style), which are not protected, and none of their marks.
4. A trademark and domain search for "Oh Really Media" is done before the name appears on a printed cover. Until then it is a working imprint.

## Consequences

- The imprint can outlive this book if the author wants it to, since it says nothing specific to Surl or Curl.
- A rights sale strips the imprint by changing one line; the manuscript needs no other edit for that.
- The parity choices that make the manuscript a drop-in for a publisher's toolchain (authoring format, numbering, house style) are separate decisions, still open.
