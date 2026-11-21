<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-log-in-over-tls
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 9 cases pass on windows.
## Findings
1. Nearest honest recipe: curl cannot be told to insist on PLAIN, so "the plain listener refusing PLAIN and naming SCRAM" is shown as curl taking SCRAM on kafka:// and naming SCRAM-SHA-512 or -256 in its refusal; `--auth PLAIN` over kafka:// offers no mechanism (exit 67). PLAIN over TLS is shown by the refusal naming SASL PLAIN.
2. RFC 4616 is not in the bibliography, so it is not cited.
