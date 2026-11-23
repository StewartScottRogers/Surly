<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-list-a-folder-page-by-page-in-process
Verdict: accept
Checked against local builds on 2026-11-22: Surl v0.1.5 (d64faa4bb) and Curl 2145a49eae (the feed's Surl.Api 1.0.0 and Curl.Api 0.2.0 reuse those version numbers), dotnet 11.0.100: all cases pass on windows.
## Findings
1. ListAsync pages in ordinal order; exact multiples end with HasMore false; limits 1 to 1000 (SurlReadRefusedException), missing folder SurlNotFoundException.
