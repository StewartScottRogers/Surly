<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-recover-a-pipeline-from-a-midway-fault-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. add_fault's close argument becomes close=graceful in the list_faults item; fault_applied for an after-bytes rule has stage accept and the listen URL as target.
3. One curl call per step, so the agent knows which step failed; curl's own --retry is in the console sibling.
