<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-prove-a-client-uses-its-proxy-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 6 of 6 cases pass on windows.
## Findings
1. Counts request heads, not exchanges, since one connection can carry several requests. RecordExchanges on; proxy via AdditionalArguments.
