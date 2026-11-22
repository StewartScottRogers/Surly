<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-find-which-account-uploaded-a-file
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 9 cases pass on windows.
## Findings
1. The -v log has no store line, so the script pairs `Login accepted: ftp <account>` and `STOR <name>` by exchange number and counts only a 2xx reply; the refused (70) upload is shown to be in the log and left out.
2. Every read of the log goes through `settled.sh` (opened equals ended, 5 s bound). Exchange numbers are matched as `\d+` because verify's readiness probe takes one.
3. `--allow-plaintext-auth` and the default log level each have a case without them.
