<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gs-read-a-gcs-object-as-a-file
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. The same store as `gs://` and `https://`; no SigV4 here, so HTTP/2 on the https side is fine and `--http1.1` is not needed.
2. https accepts the HMAC key pair as Basic and the token as Bearer; both read what gs wrote.
3. Not claimed: JSON API under `/storage/v1/`.
