<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-see-why-mail-was-refused-in-csharp
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 10 of 10, with dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0 cases pass on windows.
## Findings
1. Command and reply from server.Exchanges after WaitForEventAsync(ExchangeEnded); reasons from the Log writer's lines for that ExchangeId.
2. The record keeps the CRAM-MD5 answer as sent (name and digest); the log withholds it as ****.
