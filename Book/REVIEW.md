# Review rubric

The standard an AI reviewer applies to a recipe after `lint.py` passes and `verify.py` has run. Lint checks shape; this checks sense. The reviewer writes its findings as a short report beside the recipe (`review.md`), and the author's accept or send-back goes in `editorial/log.md`. A recipe is not `published` until it has passed all three: lint, verify, review.

## Fitness

1. Does the Solution solve the Problem exactly as stated, no more and no less? A reader with that problem, running those commands, is done.
2. Is the Problem the reader's, in the reader's words, and would a search for that problem find this recipe by its title and first sentence?
3. Is this the simplest solution the two programs offer? If an option is there only to make the test pass, say so or remove it.

## Truth

4. Is every claim in the Discussion either a case in `recipe.yml` or a plain restatement of one? A claim with no case is a guess.
5. Does the Discussion say what the test proved and nothing the test did not prove? "Always", "never" and "every" need a case or a citation.
6. Does every option used appear in the Discussion with what it does here, not what it does in general?
7. Where the server refuses something, does the Discussion say why that refusal is the default and which loosening option would change it?

## Reader

8. Can the recipe be read alone, with no other recipe open? Cross-references are pointers, not prerequisites.
9. Is every acronym spelled out on first use in this recipe, since the reader may have opened the book here?
10. Is the voice right: plain, direct, dry; the server is surly, the author is not. No exclamation marks, no "simply", no "just".

## Machine

11. Is the one-sentence Problem enough for a machine to decide, from the index alone, whether this is the recipe it wants?
12. Are the case ids and claims specific enough that a future port (to Rust, say) could implement from them without reading the prose?

## Verdict

`accept` with no findings; `accept` with minor findings the reviewer fixed itself and lists; or `send back` with the numbered findings and, for each, what would satisfy it.
