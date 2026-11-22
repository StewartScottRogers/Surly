<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-two-folders-as-shares-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 5 of 5 cases pass on windows.

## Findings

1. Same five claims as the console; a second SurlServer in the same process is refused with SurlServerStartException (Lines[0] names code 124).
