<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpcs-call-over-tls-from-an-agent
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. start_server has no TLS fields; --cert/--key/--user go in arguments, files read from SURL_MCP_ROOT (setup writes them there).
2. The 60 case matches 'curl: (60) ' then any text (platform wording).
