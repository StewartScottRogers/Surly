<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-test-an-httpclient-app-against-surl

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.

## Findings

1. The app sets its own Authorization headers (Basic, then Bearer) and they pass through CurlHttpMessageHandler unchanged; with handler.Credentials the handler answers the challenge with Digest instead.
2. An untrusted certificate is HttpRequestException with an inner CurlException PeerFailedVerification (60); expected the same on Linux and macOS (the port's own TLS client), CI checks.
