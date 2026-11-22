<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: coaps-serve-over-dtls-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 11 of 11 cases pass on windows.
## Findings
1. curl-mcp transfer over CoAP answers statusCode 0 and the payload; a 4.01 is statusCode 0, empty body, failure null, indistinguishable from an empty resource, so the response code is read with the curl tool and -i (claimed). curl-mcp's -i output uses CRLF; the program normalizes it.
