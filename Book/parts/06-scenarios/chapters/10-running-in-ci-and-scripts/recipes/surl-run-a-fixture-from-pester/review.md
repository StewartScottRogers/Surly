<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-fixture-from-pester
Verdict: accept with fixes
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
5 of 5 cases pass on Windows with the inbox Pester 3.4.0, and again with Pester 5.7.1 saved into the scratchpad and put first on PSModulePath (not installed).
1. GitHub runner images (Ubuntu 26.04, macOS 26, Windows) have pwsh 7.6 and Pester 5.9.0, so CI runs the cases on Pester 5; this machine has only 3.4. The checks throw instead of using Should, whose syntax differs between 3.4 and 5: the nearest honest file that runs on both.
2. No bibliography key exists for Pester or PowerShell; none is cited.
