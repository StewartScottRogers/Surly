<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-forward-mail-with-an-alias-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. The alias is given through AdditionalArguments; a case shows MailAliases gives the same result.
3. The no-duplicate claim (alias plus direct address) waits on a marker mail delivered last, since a missing third delivery cannot be told from a late one.
