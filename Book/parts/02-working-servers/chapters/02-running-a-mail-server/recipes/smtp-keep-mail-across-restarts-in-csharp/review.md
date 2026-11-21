<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-keep-mail-across-restarts-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. The console's restart is a kill; the C# case disposes the server cleanly, and says so in the Discussion.
3. Console flags-kept uses STORE; Curl.Api cannot (one-token HttpMethod), so the twin keeps \Seen set by a fetch instead. The console's index-on-disk case is shown as file existence.
