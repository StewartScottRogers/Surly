<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-tests-in-parallel-without-clashes
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
6 of 6 cases pass on Windows.
1. Each worker owns port (0), directory and process; the clashes (shared directory 124, shared port 45) come from clash.sh, so the Solution lists no extra server.
2. The failing worker's curl message (`curl: (22) The requested URL returned error: 401`) is matched exactly; it is the port's own text on every platform.
