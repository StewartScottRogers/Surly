<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-test-a-sensor-to-alert-flow-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. Carries the console's eight claims; the alert is read from the server's side (WaitForEventAsync on the mail store, then ListMessagesAsync and ReadMessageAsync) instead of POP3.
2. After each Curl.Api publish the program waits for ExchangeEnded after the sequence taken before the POST (mqtt.md).
3. The anonymous mailbox's owner is the empty name: the delivery event is named /INBOX/;UID=1, and ListMessagesAsync("", "INBOX") works.
