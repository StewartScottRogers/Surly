<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: nats-log-in-with-a-token-in-csharp

Verdict: needs-work (program defect: a token login is never accepted)

Checked against local builds on 2026-11-22 (not releases): Surl.Api 1.0.0 (Surl commit 5da5b3eaf), Curl.Api (Curl commit c84035f797): 12 of 12 cases pass on windows.

## Findings

1. Same defect as the console recipe (see its review); the last case records it. Carries all twelve console claims; the log is read after the server is disposed.
Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: BL-1588 fixed: right-token-accepted and loosening-accepts-the-token publish; not-in-log now counts after an accepted login. 13 of 13 pass on windows.
