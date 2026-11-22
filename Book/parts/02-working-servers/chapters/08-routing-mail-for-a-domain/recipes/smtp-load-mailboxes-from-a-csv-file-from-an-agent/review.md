<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-load-mailboxes-from-a-csv-file-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 12 of 12, with dotnet 11, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core cases pass on windows.
## Findings
1. start_server reads --mailbox-file relative to SURL_MCP_ROOT and refuses ../ before surl runs (`--mailbox-file names ../mailboxes.csv, which is not inside the root`); with -v, log_lines carry the start lines.
2. The program writes the CSV itself into its folder, as an agent with a file tool would; verify runs it in a scratch copy.
