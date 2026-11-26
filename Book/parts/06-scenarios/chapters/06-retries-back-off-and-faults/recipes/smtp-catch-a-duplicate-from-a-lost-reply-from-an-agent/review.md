<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-catch-a-duplicate-from-a-lost-reply-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): surl 1.0.0 (Surl master 1e5204d), Curl port 8.22.0 (Curl f2331ebe), Surl.Api, Curl.Api, surl-mcp, curl-mcp from the same builds: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet.
2. Differs from smtp-chaos-test-a-mail-sender's lost-reply case: counts by IMAP UID SEARCH HEADER Message-ID through the curl tool and by store_changed exchange_id, and bounds the claim with no-retry and cut-before runs; links to it for measuring the offset at run time.
