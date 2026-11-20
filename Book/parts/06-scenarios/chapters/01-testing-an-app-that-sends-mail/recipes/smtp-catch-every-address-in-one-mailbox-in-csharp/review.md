<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-catch-every-address-in-one-mailbox-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. As in the check-the-mail twin, SmtpClient plays the app, as a .NET app under test would (Curl.Api 0.2.0 can send SMTP only with a stream body; see facts/mail.md), with AllowPlaintextAuthentication; the refusal without it is pointed at that twin rather than repeated.
2. The program exits 1 when the count differs from the addresses mailed; the one-message and no-catch-all cases use that as their failure path.
