<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-forward-mail-with-an-alias-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. wait_for_event, bounded at 10 s, replaces any sleep; a seed_mail case shows the alias applies without SMTP.
3. Same nine claims as the C# twin; the C# property case has no MCP counterpart, seed_mail takes its place.
