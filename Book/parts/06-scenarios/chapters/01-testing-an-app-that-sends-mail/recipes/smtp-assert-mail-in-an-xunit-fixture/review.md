<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-assert-mail-in-an-xunit-fixture
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Nearest honest recipe: xUnit is not a pinned package (build/packages.yml), so the program holds the fixture class, two tests and a ten-line runner that does what xUnit would (fixture once, reset before each test). The sentences about how xUnit hands a class fixture to a test class are uncited: no xUnit entry in the bibliography.
2. Protocols are [smtp]: the fixture reads the store through SurlServerMail, no IMAP (the plan row says smtp, imap).
3. "Fresh mailbox" is SurlServer.ResetAsync before each test; case no-reset shows the second test failing without it.
