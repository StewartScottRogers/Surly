<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-share-a-folder-read-only

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 9 of 9 cases pass on windows.

## Findings

1. Fixed: accounts are `--user`, not `--mailbox`. A `--mailbox` account logs in only on the mail schemes, so the Surl README's example (`share-access = docs=ann@example.com:r`) gives a share no one can read over HTTP (probed: ann's Digest login and an anonymous request both get 404). Reported to the lead as a README finding.
2. Fixed: "`r` reads, `rw` writes" is now cited to the README; the recipe proves only that `readonly` beats an `rw` grant.
3. `--allow-uploads` and the root upload are there to show what `readonly` overrides; without them the 403 would prove less. Kept on purpose.
4. <<http-require-a-login>>, <<http-log-in-with-digest>> and <<http-accept-uploads>> point at recipes not yet written.
