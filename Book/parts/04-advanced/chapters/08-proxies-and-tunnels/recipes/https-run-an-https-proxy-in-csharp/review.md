<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-run-an-https-proxy-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. Two SurlServers: Surl.Api needs a non-proxy listen URL, and one server serves one certificate.
2. VerifyPeer+VerifyHost false stand for -k / --proxy-insecure.
