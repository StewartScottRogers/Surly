<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-see-which-command-broke-a-transfer
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 8 cases pass on windows.
## Findings
1. Scenario: three runs of a two-step job against `error` (STOR 451) and `drop` (RETR) rules spent once each, then the log read through `settled.sh`; the failure path is a server with no rule and no uploads (550, no rule line).
2. The script skips lines logged between `Data connection opened` and `closed`, which carry file bytes.
3. The 56 message is matched by `curl: (56)` only, since its wording may differ by platform.
