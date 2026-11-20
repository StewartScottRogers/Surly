<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-give-each-user-their-key

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.

## Findings

1. A refused key is logged as refused password and keyboard-interactive logins (curl falls back), never as a refused publickey; the claim and the events cases say so. With `SshAuthenticationTypes.PublicKey` set the events still show the fallback methods.
2. Surl.Api has no `AuthorizedKeys` property: the C# twin uses `AdditionalArguments`.
