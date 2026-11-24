<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-resume-a-ranged-download
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Cut cases are [windows, linux] (macOS mid-body reset hang, platforms.md).
3. Finding for Curl: curl's s3:// ignores -r and -C - (no Range sent, 200 whole object), and a reset on s3:// exits 2 with a .NET message instead of 56. The recipe uses https + --aws-sigv4, where both work; neither bug is claimed.
4. get-range.sh needs a POSIX shell; the received count before a cut varies, so the cases check only that each try starts where the last stopped.
