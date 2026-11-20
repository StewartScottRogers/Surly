<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-reset-by-copying-a-clean-directory

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.

## Findings

1. Protocols narrowed to http (the plan row says http, ftp): an FTP test needs a clear-text or anonymous login, a loosening this recipe does not need.
2. The script starts and kills its own surl; the 124 case uses a long-running server the recipe started.
