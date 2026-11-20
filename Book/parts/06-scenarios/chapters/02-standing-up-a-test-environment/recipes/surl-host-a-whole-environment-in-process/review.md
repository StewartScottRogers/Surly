<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-host-a-whole-environment-in-process
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. HTTP, SMTP, IMAP, MQTT and Redis on one SurlServer; AreExtensionsTurnedOn=false refuses redis with UnsupportedProtocol (1).
2. AllowPlaintextAuthentication is needed for MQTT and Redis; the strict case shows WRONGPASS (LoginDenied).
3. A Mailboxes entry with ;protocols=...,redis is refused (FailedInit, unknown scheme redis) when extensions are off, so the app uses a plain account.
4. 'No external binaries' is stated from the Surl README (in-process), not a case.
