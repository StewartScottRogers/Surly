<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-collect-mail-with-an-event-callback
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Differs from smtp-collect-mail-as-it-arrives by showing the callback API itself: Name filter, Dispose, two callbacks, order, Seeded and Reset changes, a reacting callback, a throwing callback.
2. StoreChangedEvent.Account is empty for a delivery (not the sender's login); not claimed. SurlServer.FindEvents needs RecordExchanges = true; the program avoids it (a witness callback instead).
3. "A callback that throws is contained" is shown with one throwing callback beside a normal one; what the server logs about it is not claimed.
