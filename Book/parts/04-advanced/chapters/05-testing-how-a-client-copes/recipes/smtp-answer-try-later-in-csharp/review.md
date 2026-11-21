<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-answer-try-later-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Same 11 claims as the console recipe; one program, each case picks how far it goes and reads its own lines.
2. Delivery is waited for with a StoreChanged callback and a ten-second bound; a refused send does not wait. Curl.Api reports a 451 as CurlException SendError, not a response.
