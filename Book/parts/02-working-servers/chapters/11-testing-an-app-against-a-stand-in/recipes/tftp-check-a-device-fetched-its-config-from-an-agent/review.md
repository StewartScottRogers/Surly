<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-check-a-device-fetched-its-config-from-an-agent
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 7 of 7 cases pass on windows.

## Findings

1. Waits with wait_for_event(exchange_ended, scheme tftp, after_sequence) within a 10 s budget, then get_exchanges for that exchange alone.
2. never-fetched takes the full 10 s by design.
