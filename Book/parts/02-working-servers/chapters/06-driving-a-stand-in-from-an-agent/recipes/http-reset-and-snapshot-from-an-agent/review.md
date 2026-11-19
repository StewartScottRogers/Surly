<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-reset-and-snapshot-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Title improved to "Resetting a Server Between Tests from an Agent" (7 words); key unchanged.
2. Fixed: "kept in memory" and "hello.txt and nothing else" had no case; removed or reworded. Absence is proven by 404s.
3. "The record of exchanges stays" was first matched on get_exchanges' exact request bytes, which need the port; it now uses `list_servers` `exchange_count` 4.
4. Snapshot IDs are opaque in the recipe (saved, never written as p1).
5. The `--directory` refusal is a second server in the same recipe, fixture `site/hello.txt`; its reason is the server's own message.
