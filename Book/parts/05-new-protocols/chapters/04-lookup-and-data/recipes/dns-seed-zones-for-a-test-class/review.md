<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-seed-zones-for-a-test-class
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Surl.Api adds whole zones, so a test 'adds records' as a child zone (beta.example.test) under the class's apex; the parent's names are unchanged (a case).
3. Overlaps surl-reset-keys-directory-and-zones on Snapshot/Restore; this one is DNS-only and shows the child zone, NXDOMAIN after restore, and that ResetAsync drops the class's zone too.
