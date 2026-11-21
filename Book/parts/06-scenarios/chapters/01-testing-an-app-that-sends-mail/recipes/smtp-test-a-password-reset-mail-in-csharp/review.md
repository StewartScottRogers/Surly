<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-test-a-password-reset-mail-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The app is a few lines in the program: server.Files.PutAsync for the page and Curl.Api for the SMTP send (CRAM-MD5, no loosening, since Curl 7d167fa10a).
2. The no-login claim for HTTP is shown by the solution (GET with no Authentication is 200); the --user counter-claim is cited to http-require-a-login.
