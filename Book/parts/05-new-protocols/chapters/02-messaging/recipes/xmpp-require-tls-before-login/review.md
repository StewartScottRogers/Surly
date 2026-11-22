<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: xmpp-require-tls-before-login

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf) and Curl commit c84035f797: 14 of 14 cases pass on windows.

## Findings

1. The refusal before TLS is shown with curl telnet:// -T sending the XML as raw bytes; exit 28 is the -m 3 timeout, the output is what the server had sent. Cases with stdout-matches on the stream id (random hex). Not yet seen on Linux or macOS: whether telnet:// -T behaves the same there (CI will say).
2. Facts sheet said xmpp without a certificate starts anyway; this build exits 58 (needs a certificate) unless --allow-plaintext-auth is given. Sheet updated.
