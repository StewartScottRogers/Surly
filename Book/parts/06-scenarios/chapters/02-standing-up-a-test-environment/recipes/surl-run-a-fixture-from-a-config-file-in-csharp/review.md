<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-fixture-from-a-config-file-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0 (local feed): 6 of 6 cases pass on windows.
## Findings
1. Nearest honest recipe: the file holds every option but the listen URLs, which are in code. Program finding, kept as case listen-urls-needed: SurlServerOptions with empty ListenUrls throws ArgumentException `ListenUrls names no listen URL.` even when ConfigFiles names a file with url lines; `surl -K file` starts from the file alone. Expected: the file's url lines count, as on the command line.
2. With --directory the mail store persists across runs, so the program reports the change in message count, not the total.
3. CurlResponse.StatusCode is 0 for an FTP GET when SendAsync returns (body started before the final reply); the program only checks it for >= 400. Not claimed.
4. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: no change. A ConfigFiles-only start (url lines from the file) is decided in Surl's ADR-0099 amendment of 2026-11-20 (BL-1439) but not in this build; StartAsync still throws ArgumentException, so the workaround and case listen-urls-needed stay.
