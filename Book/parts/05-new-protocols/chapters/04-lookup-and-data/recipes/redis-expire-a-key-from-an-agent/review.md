<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-expire-a-key-from-an-agent

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.

## Findings
1. set_key's expire_ms bound is enforced by the tool schema, so a bad value is an MCP invalid-params error (McpProtocolException in the SDK), not a tool result; case expire-ms-bounds shows it.
2. read_key of a key with no expiry has no expires_at field at all; the program says `none` for that.
