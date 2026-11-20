<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-a-client-to-https

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 8 of 8 cases pass on windows.

## Findings

1. verify's readiness probe opens an exchange on each port, so the record is read per request (`requests.sh`, awk over `--trace-ascii`), not per exchange.
2. 'Plain listener turned off' is a second server (HTTPS only) plus a port where nothing listens; the console cannot restart one server with other listen URLs. The C# and agent twins restart on the same address.
3. schema 2 because of `setup:` (openssl).
