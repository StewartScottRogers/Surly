<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-refuse-an-oversized-request-head-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. transfer's headers must be built as a JsonArray of JsonObject; a Dictionary array or anonymous type throws NotSupportedException in the SDK's serializer (recorded in facts/faults.md).
2. The recorded case reads the server's own bytes with get_exchanges, so the refusal is shown from both sides.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 431 now carries a 36-byte text body, `431 Request Header Fields Too Large` (BL-1444); the cases and the Discussion say so.
3. Bounded wait before reading the server's record (CI race sweep).
