<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-list-and-make-folders-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. One program, three in-process servers; the cases match lines of its output.
2. Curl.Api has no names-only listing (`curl -l`, CURLOPT_DIRLISTONLY): the program reads the names from the long listing. Not carried over from the console twin: the long-listing format claim and the no-login refusal (exit 67).
3. SFTP quote commands live in `CurlFtpOptions.Quote`, an FTP-named record; `CurlSshOptions` has none. It works, but a reader will not look there first.
4. The start-up warning of `ThrowawayHostKey` is not shown (it goes to `Log`, unused here).
