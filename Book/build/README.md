# Build, verify, lint

## Once, on any machine

```
npm install -g asciidoctor          # the reference renderer (Node)
pip install pyyaml                  # the scripts' one dependency
```

Portable Document Format and EPUB need the Ruby renderers (`gem install asciidoctor-pdf asciidoctor-epub3`); the build adds them once they are decided.

## Every time

```
python3 build/lint.py     # shape: sections, anchors, copyright, options explained, references resolve
python3 build/verify.py   # truth: runs every case against surl and curl, writes verified.yml per recipe
python3 build/build.py    # the book: generates each recipe's blocks, run.sh, run.ps1, the spine, index.json, by-protocol, and renders out/
```

`verify.py` finds `surl` and `curl` on PATH, or takes `SURLY_SURL` and `SURLY_CURL`. It accepts recipe keys to run a subset. A recipe stays `draft` until it has been verified against released binaries; the book says so under each Solution.

`build/recipes.py` is the shared definition of a recipe (folders, `recipe.yml`, placeholders) that all three scripts import.

Not committed: `generated/`, `out/`, and each recipe's `.generated/`. Committed: each recipe's `run.sh`, `run.ps1`, fixtures and `verified.yml`.

## Commit signing (once, the author)

Run these two commands once on the machine you commit from; they create a key tied to your GitHub email and tell git to sign every commit with it.

```
ssh-keygen -t ed25519 -C "Stewart.Rogers@pobox.com" -f ~/.ssh/surly-signing -N ""
git config --global gpg.format ssh && git config --global user.signingkey ~/.ssh/surly-signing.pub && git config --global commit.gpgsign true
```

Then add `~/.ssh/surly-signing.pub` to GitHub as a **signing key** (Settings > SSH and GPG keys > New SSH key > Key type: Signing Key) so GitHub shows commits as Verified. Tick the box in `../RIGHTS.md`.
