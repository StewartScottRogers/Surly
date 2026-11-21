<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-a-byte-range-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Curl.Api has no range option (no CURLOPT_RANGE); the Range header goes in CurlRequest.Headers, as the plan asked.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 416 now carries the body `416 Range Not Satisfiable` (BL-1444), 26 bytes; past-the-end and the Discussion restated.
