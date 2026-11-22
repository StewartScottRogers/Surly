<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-sync-a-folder-to-a-bucket-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. Same sync as the C# twin, through curl-mcp's `curl` tool; puts counted with `get_events` (`store_changed`, `store` `content`, events with an `exchange_id`).
2. Console's missing-source case has no counterpart (the failure is the program's own file read); a case that a sync with nothing changed puts nothing stands in for it.
