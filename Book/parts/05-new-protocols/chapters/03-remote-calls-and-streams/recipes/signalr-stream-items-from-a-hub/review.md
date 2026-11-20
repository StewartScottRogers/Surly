<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: signalr-stream-items-from-a-hub

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.

## Findings
1. Truth: every claim in the Discussion is a case; the loosening (--allow-anonymous) has its refusal (case refused-by-default, 401) and its use.
2. Count with a text argument (`x`) is also refused with Invalid arguments (probed); only -1 is a case, and the prose names only -1.
