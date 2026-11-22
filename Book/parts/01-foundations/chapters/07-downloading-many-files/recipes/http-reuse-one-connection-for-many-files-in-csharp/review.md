<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-reuse-one-connection-for-many-files-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. Awaited in turn, so counts are exact. A warm-up request on its own client comes before the count.
2. CurlNetworkOptions.ForbidReuse and FreshConnect also give three connections (probed, not claimed).
3. Bounded wait before reading the server's record (CI race sweep).
