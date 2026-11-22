<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-check-an-app-uploaded-the-right-file
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 7 cases pass on windows.
## Findings
1. The check compares bytes with `cmp -s` and prints its own line with `wc -c` sizes, because GNU and BSD `cmp` word their output differently (`byte` against `char`).
2. FTP and HTTP need two ports on one surl (both TCP); the store is in memory so no stale file from an earlier run can pass the check.
3. With uploads off the first refusal is MKD (`--ftp-create-dirs`), exit 9, not 25; the text says so.
