<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-stop-a-server-and-read-its-exit-code
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Nearest honest recipe: no case makes StopAsync return anything but Ok. The documented failure value, InternalError, cannot be provoked with these builds; the Discussion says only Ok is shown.
2. Found while probing: StopAsync waits about 7 s when a CurlClient still holds a kept-alive connection, a few ms once clients are disposed. Claimed with a 3 s threshold either side; the program disposes each client after its fetch.
3. That the wait is surl's Ctrl+C shutdown grace period is from the StopAsync XML documentation; the case shows only that it is over 3 s.
