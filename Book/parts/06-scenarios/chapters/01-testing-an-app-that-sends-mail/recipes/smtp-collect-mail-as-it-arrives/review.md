<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-collect-mail-as-it-arrives
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. OnEvent(StoreChanged, Mail) callback parses StoreChangedEvent.Name (owner/mailbox/;UID=n) and reads the message with ReadMessageAsync; a TaskCompletionSource ends the wait, with a 2 s limit (not polling).
2. Delivery order in the list relies on the callback running one event at a time (OnEvent's documented behaviour, cited to surl-readme).
