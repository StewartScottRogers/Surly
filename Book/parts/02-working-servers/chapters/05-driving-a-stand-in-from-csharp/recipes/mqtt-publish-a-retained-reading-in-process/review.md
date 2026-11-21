<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-publish-a-retained-reading-in-process
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. No program finding. A seed is kept with no filter and no login; a client publish is not kept; the wait for ExchangeEnded takes its cursor before the POST.
2. Overlaps mqtt-keep-a-retained-message-in-csharp on seed and replace; this recipe adds payload shapes (binary, 100,000 bytes), a wildcard over rooms and the login refusal.
