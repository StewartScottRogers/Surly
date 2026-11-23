<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-test-a-shared-team-folder-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Program finding: Curl.Api 0.2.0 cannot IMAP APPEND. PUT with CurlContent.FromStream(stream, length) -> CurlException 25 "Cannot APPEND with unknown input file size"; StreamContent sets POSTFIELDSIZE, not INFILESIZE. Expected: the append succeeds with the given or seekable length. Case curl-append.
2. Program finding (API shape): Curl.Api has no CUSTOMREQUEST beyond HttpMethod, which refuses spaces, so IMAP STORE/COPY/MOVE with arguments cannot be sent (FormatException). Case curl-store.
3. Because of 1 and 2 the program speaks raw IMAP (CRAM-MD5 by hand) for APPEND and STORE; nearest honest recipe. Curl.Api does the reading.
4. SurlServerMail.DeliverAsync to team@ throws NoSuchAccount where SMTP accepts and discards; both stated.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: unchanged, 5 of 5 pass. APPEND is still impossible: BL-5594 left imap out, and BL-5610 (imap upload) and BL-5612 (CustomRequest for STORE) came after 7d167fa10a. Probed: PUT with a byte body to imap://.../INBOX throws QuoteError 21; with FromStream(stream, length) still 25 Cannot APPEND with unknown input file size. The hand-spoken IMAP stays.
Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: Curl BL-5753: a Curl.Api PUT with a known-length body now APPENDs; the test appends with Curl.Api, the raw-IMAP APPEND and the curl-append refusal case are gone; STORE is still spoken raw. 4 of 4 pass on windows.
