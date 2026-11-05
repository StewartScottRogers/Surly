# Build

Build scripts for the book arrive with the authoring-format decision. For now this folder holds the one setup step the author does himself.

## Commit signing (once)

Run these two commands once on the machine you commit from, replacing nothing; they create a key tied to your GitHub email and tell git to sign every commit with it.

```
ssh-keygen -t ed25519 -C "Stewart.Rogers@pobox.com" -f ~/.ssh/surly-signing -N ""
git config --global gpg.format ssh && git config --global user.signingkey ~/.ssh/surly-signing.pub && git config --global commit.gpgsign true
```

Then add `~/.ssh/surly-signing.pub` to GitHub as a **signing key** (Settings > SSH and GPG keys > New SSH key > Key type: Signing Key) so GitHub shows commits as Verified. Tick the box in `../RIGHTS.md`.
