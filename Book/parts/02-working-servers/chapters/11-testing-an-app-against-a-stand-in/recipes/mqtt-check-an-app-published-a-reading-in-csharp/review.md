<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-check-an-app-published-a-reading-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows (C#).

## Findings

1. Carries all eleven console claims. The app's publish waits for ExchangeEnded after the last sequence seen, bounded at 10 s; reads of the store follow it. No sleep.
2. The check is ReadRetainedAsync with no client; the console's poll becomes the event wait.
