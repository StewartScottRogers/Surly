<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-review-an-apps-mail-against-rules
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The rules are the agent's own code over read_message's text; the catch-all mailbox gathers every recipient.
2. Finding: surl stores only Return-Path and Received of its own; no Date or Message-ID is added, so a missing one is the app's (cased, server-adds-no-date).
3. Removed uncased claims (start warning of --mail-catch-all, read_message setting no flag); the warning text is in facts/mail.md.
