<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpcs-call-over-tls
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. Wrong password and no login are refused at curl's reflection request: stderr 'The server did not answer gRPC server reflection (grpc-status 16 (UNAUTHENTICATED): unauthenticated)'.
2. Plain grpc with -u: grpc-status 7 PERMISSION_DENIED, the reason the client stays on TLS.
3. Exit 60 wording is the TLS library's; cases match 'curl: (60)' only. No TLS 1.3 claim, so no macOS split (default range negotiates 1.2 there).
4. Not claimed: plain grpc:// to the grpcs port gave exit 55 'Connection was reset' on Windows; not probed elsewhere.
