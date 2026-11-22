<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-keep-readings-across-restarts-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows (C#).

## Findings

1. Carries the console claims; restart is DisposeAsync and a second SurlServer on the same DataDirectory (a clean stop, not a crash), with its own port and event sequence. Adds a seeded reading that survives.
2. Publishes wait for ExchangeEnded, bounded at 10 s, before the broker is disposed. The program disposes the broker before deleting the folder: on Windows a held lock file makes the delete throw.
