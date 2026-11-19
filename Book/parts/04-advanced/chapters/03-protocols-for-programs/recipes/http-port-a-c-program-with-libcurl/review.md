<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-port-a-c-program-with-libcurl
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), dotnet 11.0.100, Libcurl 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Fixed: removed the claim that `SetOption` answers `CURLE_BAD_FUNCTION_ARGUMENT` for a wrongly typed value (true when probed, but no case; a case would need a deliberately broken mode in a program meant to be a faithful port). The Discussion now says only that the port should check `SetOption`'s `CurlCode` as C checks `curl_easy_setopt`.
2. Fixed: dropped `curl-manual` from references; it is the tool's man page, not libcurl's API. Suggest a `libcurl-api` key for `curl_easy_*`.
3. The C listing in the Discussion is not compiled or run by verify; only the C# port is. It is the standard libcurl example shape and the port mirrors it line for line, but an author check is wanted.
4. The C program's `CURLOPT_WRITEDATA` line has no C# counterpart: the lambda captures standard output. The Discussion says why.
5. `EasyInit` never returns null, so the C null check is not ported; stated in the Discussion.
6. Case nothing-listening assumes nothing listens on 127.0.0.1 port 1.
