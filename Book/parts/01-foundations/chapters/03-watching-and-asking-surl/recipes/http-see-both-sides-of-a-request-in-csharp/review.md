<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-see-both-sides-of-a-request-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. Observation for Curl: Curl.Api sends `Content-Type: application/x-www-form-urlencoded` with a PUT that has a Body (curl -T sends none). Not claimed.
2. The program prints only the first comma-separated part of Authorization from Exchanges, which hold it in clear by design.
