<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-build-urls-with-the-libcurl-url-api
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Libcurl 0.2.0: 11 of 11 cases pass on windows.

## Findings

1. The C listing shows `parse` and `join` in full and `build` as a comment, to keep it readable; the C# port has all three.
2. No-scheme answer is CURLUE_BAD_SCHEME (27) in this port; upstream curl 8.22.0 is believed to agree, not checked against upstream here.
