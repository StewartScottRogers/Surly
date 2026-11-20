<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fetch-a-file-with-an-awkward-name-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. build_url does not encode by itself: without the UrlEncode flag it keeps a space and a # as given, so the URL it returns is invalid or points at the wrong file. It encodes with the flag; the recipe says so.
2. build_url answers text JSON {url, failure, isError}; flags are libcurl CURLU names without the prefix.
