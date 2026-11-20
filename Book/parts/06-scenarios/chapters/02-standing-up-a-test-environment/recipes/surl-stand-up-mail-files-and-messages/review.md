<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-stand-up-mail-files-and-messages
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The app's three sends are `seed:`; the solution is the test's three reads in one curl.
2. `--allow-plaintext-auth` is needed only for MQTT; the strict server shows the refusal and a case shows SMTP still uses CRAM-MD5 with it on.
3. MQTT subscribe output carries a two-byte length; matched with `stdout-matches`.
