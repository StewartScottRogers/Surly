<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-read-answers-as-json-from-an-agent

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 9 of 9 cases pass on windows.

## Findings
1. curl-mcp's transfer tool reads a dns URL and answers the dig text, statusCode 0; it has no --dns-json field (case transfer-has-no-json-switch).
2. The program's second add_dns_zone for the same apex is a tool error and leaves the first zone in place (case second-zone; the zone is not read back in that case).
