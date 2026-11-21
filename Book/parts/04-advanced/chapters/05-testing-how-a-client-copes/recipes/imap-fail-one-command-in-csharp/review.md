<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-fail-one-command-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. Curl.Api has no retry option; the retry case is the app's own loop, and the console's --retry-alone case has no counterpart (no-retry is the nearest).
3. SELECT answered NO maps to CurlException LoginDenied (67) 'Select failed': noted in the recipe.
