<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-files-from-memory
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. "No disk at all" from the task is not claimed: no case can show it (as in http-serve-and-fetch-in-process).
2. A dot-name key (docs/.draft) is accepted by StartAsync and then never served; worth knowing, claimed as observed.
3. Keys a//b and c:x are also refused (probed); not cases, to keep the table short.
