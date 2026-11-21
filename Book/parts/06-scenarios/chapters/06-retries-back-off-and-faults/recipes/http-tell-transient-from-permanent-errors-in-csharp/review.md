<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-transient-from-permanent-errors-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows, one claim per case (a label argument prints one line).
## Findings
1. Policy in the program (Curl.Api has none); requests counted per path in the verbose Log and FaultApplied events per RuleId.
