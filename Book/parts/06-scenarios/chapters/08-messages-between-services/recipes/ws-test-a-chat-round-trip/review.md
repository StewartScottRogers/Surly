<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-test-a-chat-round-trip
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. surl --ws-echo (AdditionalArguments; no SurlServerOptions property) is the chat stand-in.
2. The server record (Exchanges[0].Sent, read after ExchangeEnded) is parsed as unmasked frames: three text frames in order and close 1000.
3. Failure paths: --max-message 1k answers close 1009; without --ws-echo the upgrade is refused, CurlException 22.
