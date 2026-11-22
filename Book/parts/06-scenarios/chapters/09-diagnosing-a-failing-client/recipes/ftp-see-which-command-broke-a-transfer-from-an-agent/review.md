<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-see-which-command-broke-a-transfer-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 8 cases pass on windows.
## Findings
1. plan.yml gives this key the same title as its console sibling; recipe.yml says `Seeing Which FTP Command Failed from an Agent` (lead to update the plan row).
2. `fault_applied` events are joined to exchanges by `exchange_id`; the record is awaited by polling started and ended counts, 10 s bound.
