# ADR-0004: One AsciiDoc source, recipes keyed not numbered, ordered by difficulty, indexed for machines

## Status

Accepted, 2026-11-05.

## Context

The book is written by AI for twenty years or more, grows by addenda, must be derivable into any format a publisher or a future tool wants, and has two readers: a person who opens it at any page, and an AI that must find one fact by the shortest route without reading the rest.

## Decision

### Source

1. **AsciiDoc is the single source.** Every rendered form (Hypertext Markup Language, Portable Document Format, EPUB, and anything later) is derived from it by the build; none is edited by hand. Asciidoctor renders it; the Node build of Asciidoctor is the reference renderer so the build runs anywhere Node runs.
2. **A recipe is a folder.** `recipe.adoc` plus everything it needs: the files its commands use, `run.sh` and `run.ps1` holding the exact commands, `expected.txt` holding the output they must produce, and `images/`. Nothing a recipe needs lives outside its folder except the two programs and the bibliography.
3. **A recipe has a stable key**, the folder name, such as `http-serve-a-directory`. The key never changes once published. The printed number ("Recipe 3.2") is generated from position at build time; moving or inserting a recipe changes numbers, never keys, and every cross-reference uses the key.
4. **A recipe carries structured metadata** as AsciiDoc attributes at its top (`recipe-key`, `protocols`, `tier`, `surl-options`, `curl-options`, `exit-codes`, `verified-surl`, `verified-curl`, `references`, `status`). The build reads them; nothing is indexed by hand.
5. **A recipe has a fixed body**: Problem, Solution, Discussion, See Also, in that order, under those headings. Every file in the book carries the copyright line.

### Structure

6. **Parts are difficulty tiers, not protocols.** Foundations; Working Servers; Secure Servers; Advanced; and a last, open-ended part for new and extended protocols. A protocol's recipes sit wherever their difficulty puts them, so one protocol may appear in several parts.
7. **Chapters group recipes by task within a tier**, and recipes within a chapter run from simplest to hardest. Folders sort by a two-digit prefix; the prefix is the only thing a renumbering touches.
8. **A new protocol lands by rule, not by judgement call**: its first recipe goes in the last part under a chapter named for the protocol, and recipes move forward into the tiered parts once the protocol is stable in both programs. The rule is written in the instructions file so an AI can apply it alone.

### Machines

9. **The build emits a machine index** (`generated/index.json`): one entry per recipe with its key, title, problem sentence, metadata, path and rendered address. An AI reads the index and then one recipe; it never reads the book.
10. **`llms.txt` at the book root** maps what exists and where, in the form already used by the Curl repository.
11. **Every recipe renders to its own address** keyed by its stable key, as well as into the whole book.
12. **Indexes by protocol, option, exit code and tier are generated** from the metadata for both readers.

### References

13. **Off-site sources live in `references/bibliography.adoc`** under stable citation keys; a recipe cites keys, never addresses. Where the licence allows, a local copy of the source is kept in `references/archive/` so the reference outlives the site.

### Verification

14. **A recipe is verified, not trusted.** `run.sh` is executed against the `surl` and `curl` builds pinned in `verified-surl` and `verified-curl`; its output must match `expected.txt`. A recipe whose verification fails is marked `status: broken` and the book still builds, with the mark shown. The verify script is a separate decision and is not yet written.

## Consequences

- Twenty years of addenda change no key and break no link.
- An AI finds a recipe in two reads: the index, then the recipe.
- The schema in item 4 is the contract the build, the verify and the instructions file all read; changing it is a new decision record.
