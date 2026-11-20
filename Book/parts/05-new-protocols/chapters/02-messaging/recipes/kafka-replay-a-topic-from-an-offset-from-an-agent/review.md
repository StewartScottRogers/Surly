<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-replay-a-topic-from-an-offset-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. The agent produces two ways, `publish_message` and the curl tool with `-d`; `read_log` shows both in one log with offsets. A curl-mcp consume of an offset past the end is a tool error (isError true) with curl exit 8, an offset equal to the count is not.
2. `read_log` past the end is a tool error `Offset 9 is outside the log, which holds 0 to 5.`, not an empty page; the Discussion says so.
3. The program names its two curl wrappers `Run` (setup, no output) and `Curl` (prints), so the produce calls leave no lines in the output.
