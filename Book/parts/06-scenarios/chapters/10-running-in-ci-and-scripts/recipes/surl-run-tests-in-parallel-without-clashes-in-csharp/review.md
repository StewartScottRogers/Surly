<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-tests-in-parallel-without-clashes-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
4 of 4 cases pass on Windows.
1. Workers keep their files in each server's own memory rather than a directory; the DataDirectory clash is shown separately.
