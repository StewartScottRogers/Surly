<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-offer-a-share-to-ftp-only
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 8 cases pass on windows.
## Findings
1. `--share "drop=drop;protocols=ftp"`: HTTP 404 for the file and the folder; `ftps` admitted (`--self-signed`, `-k`); an unknown scheme is exit 2 `unknown scheme nope` (no `(2)` prefix).
2. Needs three ports (ftp, http, ftps).
