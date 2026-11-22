<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-a-byte-range
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 10 of 10 cases pass on windows.
## Findings
1. Planned task said several ranges get the whole file with 200; surl answers 206 multipart/byteranges (boundary SURL_BYTERANGES), one part per range. Recipe shows what surl does; the plan's expectation was wrong, not surl (RFC 9110 allows either).
2. The multipart body has CRLF line ends; the case allows \r? because verify reads stdout as text.
3. '-r sends a Range header' and 'the server may ignore an invalid range' are cited to rfc9110, not cases.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 416 now carries the body `416 Range Not Satisfiable` (BL-1444), 26 bytes; past-the-end and the Discussion restated.
