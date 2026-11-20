<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-find-the-port-it-chose-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. BoundUrls and BoundUrl are Uri values, not strings; the program interpolates them.
2. The bind-failure message ends in the platform's reason (Windows: Address not available), so that case is a pattern.
3. The XML documentation says StartAsync returns once every listener has bound; stated, not a separate case (the fetches right after it are).
