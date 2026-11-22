<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-read-a-files-size-and-date-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Curl.Api has no property for curl's FILETIME, and HEAD on an FTP URL sends SIZE only, so the program sends MDTM as a Quote command and reads the 213 reply from Headers (every control reply is a header whose key is the reply line). Worth a Curl.Api property; for the author.
2. Missing file with the MDTM quote throws QuoteError (21), not RemoteFileNotFound (78): the quote fails first.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: CurlClientOptions.FetchFileTime (BL-5603) replaces the MDTM quote; HEAD sends MDTM then SIZE and Headers carries Last-Modified and Content-Length as curl -I prints them; a missing file is now RemoteFileNotFound 78, as on the console (was QuoteError 21). Program finding: CurlResponse.Info.FileTime stays -1 with FetchFileTime on (README says it holds the time), so the program reads Last-Modified. 5 of 5 pass.
3. Bounded wait before reading the server's record (CI race sweep).
