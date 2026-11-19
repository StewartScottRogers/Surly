<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: rtsp-play-a-stream-with-libcurl
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), dotnet 11.0.100, Libcurl 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Fixed: the session ID changes every run, so the program now writes it to standard error and stdout is exact; the "16 hexadecimal digits" claim went with it (only ADR-0074 says so, not the README).
2. Fixed: claims and prose no longer say the first packet carries 1400 bytes, that libcurl checks `CSeq`, or that the RTCP packet is a sender report and BYE: no case observes them. The marker-bit stop is proven, since a misplaced marker would end the loop with the wrong count or bytes.
3. Fixed: regex expectations replaced with exact stdout so the claims table renders on one line.
4. `site/clip.txt` is a 1500-byte YAML fixture reused by anchor (`&clip`/`*clip`) for the byte-for-byte check, so the file content appears once.
5. `CURLOPT_RTSP_STREAM_URI` is set once to the file's URL and used for every step; surl accepts the presentation URL on `PLAY` and `TEARDOWN` as well as `*`.
6. The 404 and 461 exits (22) are the program's choice, modelled on `curl -f`; the Discussion says so.
7. Bibliography: as for ws-echo-messages-with-libcurl, a `libcurl-api` key would let the recipe cite `CURLOPT_RTSP_REQUEST` and `CURLOPT_INTERLEAVEFUNCTION`.
