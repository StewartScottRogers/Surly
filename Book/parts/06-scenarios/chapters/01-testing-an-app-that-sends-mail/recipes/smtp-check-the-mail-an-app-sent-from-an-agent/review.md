<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-the-mail-an-app-sent-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Program note, not a finding against surl or curl: a file-based app cannot use JsonSerializer.Serialize on a string (reflection serialization is off by default), so the program prints JsonElement.GetRawText instead.
2. get_exchanges' reply line for an unknown in-domain recipient is matched as '250 ...' rather than exactly, to leave its enhanced code free.
