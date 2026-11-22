<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: nats-stand-in-for-a-service
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. curl has no NATS responder, so the stand-in is a NATS session written line by line into curl -T - over telnet://; it waits for PONG (SUB taken) before the app's ?request runs, and for the MSG line (bounded) before it answers.
2. Without -T -, curl telnet:// on Windows sent standard input with each CR LF turned into LF, and surl answered -ERR 'Parser Error' to the payload; upstream curl sends stdin bytes unchanged. Reported to the lead as a possible Curl finding; not claimed.
3. The server log (-v --log-file) is polled with a bound for the app's PUB and the stand-in's reply.
4. Silent stand-in uses a held server (--fault delay on PUB) so -Z puts the subscriber in place first, as nats-publish-and-subscribe does.
