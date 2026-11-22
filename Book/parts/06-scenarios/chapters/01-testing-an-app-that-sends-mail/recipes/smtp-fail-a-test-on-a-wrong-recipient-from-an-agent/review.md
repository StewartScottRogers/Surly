<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-fail-a-test-on-a-wrong-recipient-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. seed_mail obeys --mail-domain (RelayingDenied), unlike SMTP's silent discard for an unknown in-domain address, where seed_mail answers NoSuchAccount; only the first is claimed here, the second is in the fact sheet.
2. Bounded wait before reading the server's record (CI race sweep).
