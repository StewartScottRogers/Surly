<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: stomp-join-a-topic-across-two-protocols-from-an-agent
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 6 of 6 cases pass on windows.

## Findings

1. The MQTT 5 subscriber is a curl tool call left running (not awaited) writing got.txt; the agent reads the file with FileShare.ReadWrite.
2. read_retained_message polled for at most 10 s.
