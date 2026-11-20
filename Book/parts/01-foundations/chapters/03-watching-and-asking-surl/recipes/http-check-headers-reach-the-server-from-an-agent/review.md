<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-check-headers-reach-the-server-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. transfer cannot drop a default header: an empty Accept value replaces curl-mcp's Accept: */* with an empty Accept: at the end of the head; curl -H "Accept:" drops it. Stated as a difference, not a bug.
2. headers must be passed as JsonNode/JsonElement: the SDK's source-generated serializer rejects List<Dictionary<string,string>> (NotSupportedException) and reflection serialization is disabled in file-based apps.
