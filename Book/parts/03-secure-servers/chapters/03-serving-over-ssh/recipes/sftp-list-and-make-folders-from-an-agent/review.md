<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-list-and-make-folders-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. One program, three servers; the cases match lines of its output. Beyond the console twin: `list_files` reads the made folders back from the server's store.
2. Program finding, not a defect: the existing-folder failure says `Operation failed` under surl-mcp's run and the case matches `[^\n]*` after `failed:`, since the console twin only checks `mkdir` in the message.
3. curl-mcp's `transfer` tool has no SFTP-specific option, so every call goes through the `curl` tool. The start-up warning of `--throwaway-hostkey` is cited to the README.
