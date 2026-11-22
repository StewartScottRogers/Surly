<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-send-to-several-recipients-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. get_exchanges shows 3 RCPT TO and one DATA; a stored-once log line is not reachable from MCP after start, so the exchange is the evidence.
3. curl tool non-zero exit is a tool error but the answer text is still JSON.
4. Bounded wait before reading the server's record (CI race sweep).
