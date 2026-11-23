<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-serve-two-mail-domains-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The discarded address is shown by a 2 s bounded wait that times out and list_mailboxes' tool error `No mailboxes for cy@two.test.`
