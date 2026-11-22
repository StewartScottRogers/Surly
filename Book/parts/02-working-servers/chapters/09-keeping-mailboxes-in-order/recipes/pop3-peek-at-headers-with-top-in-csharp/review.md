<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-peek-at-headers-with-top-in-csharp
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. TOP's two arguments go in the URL path (`TOP` + `/2%200`), the only way with Curl.Api 0.2.0's one-token methods; stated in the Discussion.
2. DeliverAsync adds no Return-Path/Received, so headers are 73 bytes; the console twin's SMTP-delivered headers are longer.
3. Program finding: Curl.Api `new HttpMethod("UIDL")` with the path `2` (UIDL 2) did not return within 60 s under verify, while curl -X "UIDL 2" returns at once; that case was dropped. Expected an empty body, as the CLI gives.
4. Bounded wait before reading the server's record (CI race sweep).
