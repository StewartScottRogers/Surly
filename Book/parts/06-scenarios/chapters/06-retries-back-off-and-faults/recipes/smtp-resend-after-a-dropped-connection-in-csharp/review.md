<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-resend-after-a-dropped-connection-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Same 11 claims as the console recipe; in-process there is no readiness connection, so a plain times=1 accept-stage drop works.
2. Delivery is waited for with a StoreChanged callback and a ten-second bound. The RecvError (56) on a dropped connection is seen on Windows; Linux and macOS not yet seen (a drop at accept may differ), CI will say.
3. CI run 44898744862: Linux Curl.Api got RecvError on one accept-stage drop and CouldntConnect (7) on the next; the greeting and twice cases (solution, drop-fails-without-retry, dropped-stores-nothing, retry-resends) accept either code, and the Discussion says why; the DATA drop stays RecvError everywhere.
