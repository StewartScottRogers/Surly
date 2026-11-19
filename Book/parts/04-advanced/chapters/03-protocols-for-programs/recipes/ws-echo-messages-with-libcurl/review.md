<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-echo-messages-with-libcurl
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), dotnet 11.0.100, Libcurl 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Fixed: `CurlOption.ConnectOnly` was first set to the `ConnectOnly.WebSocket` enum; Libcurl refuses an enum for a `CURLOT_LONG` option with `BadFunctionArgument`, the program ignored the return as C programs do, `Perform` read a download instead of stopping at the upgrade, and four cases hung. The program now passes `2L`, as C passes `2`. Raised with the author as API friction (see report).
2. Fixed: dropped "the default limit is 1 mebibyte" (from `--aihelp`, no case) and reworded the `CurlCode.Again` sentence as what the program does.
3. Fixed: `--directory` named in the Discussion (lint).
4. The 64-byte receive buffer is deliberately small so the reassembly loop shows; the Discussion says so. A reader's real buffer would be larger.
5. The program polls `curl_ws_recv` with a 10 ms sleep on `CurlCode.Again`. A C program would wait on `CURLINFO_ACTIVESOCKET`; Libcurl offers no socket to wait on, so polling is the honest port.
6. The libcurl API pages (`curl_ws_send`, `curl_ws_recv`, `CURLOPT_CONNECT_ONLY`) are not in the bibliography; the recipe names the calls without citing them. Suggest a `libcurl-api` key (https://curl.se/libcurl/c/).
7. Case nothing-listening assumes nothing listens on 127.0.0.1 port 1, as http-fetch-from-a-surl-process does.
