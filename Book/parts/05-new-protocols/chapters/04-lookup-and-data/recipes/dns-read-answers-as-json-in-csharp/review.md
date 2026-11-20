<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-read-answers-as-json-in-csharp

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.

## Findings
1. Program finding: Curl.Api 0.2.0 has no property for --dns-json; the response body is the dig-like text. The program parses that text into JSON itself. The server's own JSON is shown on the console and agent recipes.
2. Program finding: Zones.ReadAsync writes a TXT string's spaces as backslash 032 (`"v=spf1\032mx\032-all"`). Zone text read back is valid master-file syntax; shown in case read-back.
3. Fixed on the way: JsonSerializer.Serialize of anonymous types throws in a file-based app (reflection disabled); the program uses JsonObject and JsonArray.
