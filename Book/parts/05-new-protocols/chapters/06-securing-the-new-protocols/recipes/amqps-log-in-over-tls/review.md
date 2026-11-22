<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqps-log-in-over-tls

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 9 of 9 cases pass on windows.

## Findings
1. Only Windows was run; Linux and macOS TLS handshake against amqps not yet seen (CI checks).
2. curl has no --allow-plaintext-auth; the loosening is surl's, on its own server.
