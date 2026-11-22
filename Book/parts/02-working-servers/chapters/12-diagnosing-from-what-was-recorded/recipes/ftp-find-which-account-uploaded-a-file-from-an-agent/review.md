<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-find-which-account-uploaded-a-file-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 7 cases pass on windows.
## Findings
1. The uploader is found by joining `store_changed` (name filter) to `login_judged` on `exchange_id`, after polling started and ended counts with a 10 s bound.
2. `start_server` refuses `--log-file` (the console's log has no MCP equivalent); `get_exchanges` keeps PASS in clear.
