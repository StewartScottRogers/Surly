<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-catch-every-address-in-one-mailbox
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 8 of 8 cases pass on windows.
## Findings
1. One message to several unknown addresses is stored in the catch-all once (case one-message-many-addresses); the prose states it as messages counted, not addresses.
2. `-X STAT`'s reply is not printed by curl; the case reads it from -v on standard error.
3. 'includes itself' is surl's own word for a catch-all whose address has no mailbox; the prose gives the plain reading (that address is itself unknown) and the fix (name a --mailbox).
