<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-test-a-shared-team-folder-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Program finding: Curl.Api 0.2.0 cannot IMAP APPEND. PUT with CurlContent.FromStream(stream, length) -> CurlException 25 "Cannot APPEND with unknown input file size"; StreamContent sets POSTFIELDSIZE, not INFILESIZE. Expected: the append succeeds with the given or seekable length. Case curl-append.
2. Program finding (API shape): Curl.Api has no CUSTOMREQUEST beyond HttpMethod, which refuses spaces, so IMAP STORE/COPY/MOVE with arguments cannot be sent (FormatException). Case curl-store.
3. Because of 1 and 2 the program speaks raw IMAP (CRAM-MD5 by hand) for APPEND and STORE; nearest honest recipe. Curl.Api does the reading.
4. SurlServerMail.DeliverAsync to team@ throws NoSuchAccount where SMTP accepts and discards; both stated.
