<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-a-clean-close-from-a-reset-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): Surl master 1e5204d, Curl master f2331ebe (surl-mcp, curl-mcp): 6 of 6 cases pass on windows, three runs.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case; the macOS note is platforms.md (Curl BL-5799).
2. Rules are added with add_fault after start_server (close as the tool's argument), so no readiness connection is involved; per-listener scope keeps the two endings on one server.
3. fault_applied and exchange_ended look the same for both endings; the agent maps rule_id to list_faults' item to learn the close. surl-mcp has no tool for the verbose log.
