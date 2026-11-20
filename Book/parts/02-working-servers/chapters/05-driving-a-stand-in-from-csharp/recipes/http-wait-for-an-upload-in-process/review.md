<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-wait-for-an-upload-in-process
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Fixed: the first draft of the `late` case printed exactly what the solution prints, so it could not show that the wait began after the upload. That mode now prints the PUT status before it waits. Also removed "at once", since no case measures time.
2. Fixed: "content type from the name" is now "text/csv here", because no case shows how the type is chosen.
3. Reader friction: `StoreKind` and `StoreChangedEvent` are in `Surl.Protocol.Abstractions`. `WaitForEventAsync` returns the base `SurlEvent`, so reading the path needs a cast. The Discussion says both.
4. Reader friction: a StoreChangedEvent made by an HTTP upload has `Scheme` and `Account` null, although, by the source (`SurlEventFacts`), a query with `Scheme = "http"` or `Account = "alice"` still matches it through its exchange. That was not run. A test that prints or filters the event's own `Scheme` gets nothing. Not claimed here.
5. Reader friction: with recording off, the message of the `InvalidOperationException` from `WaitForEventAsync` says "to read Exchanges", which is misleading for a wait. The program prints its own line instead.
6. The Basic 403 rests on `--allow-plaintext-auth` (surl --aihelp auth). The loosening itself has no case here.
7. Cross-reference <<http-wait-for-an-event-from-an-agent>> points at a recipe not written yet (chapter 11).
8. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: the solution failed once in the full run (PUT answered 201 after a 5 s wait had timed out) and passed alone; the Digest PUT was slower than 5 s on a loaded machine. The wait that expects an event now allows 30 s; it returns as soon as the event comes. Not a surl change.
