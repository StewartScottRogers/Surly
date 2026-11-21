<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-copy-a-report-from-ftp-to-s3-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Nearest honest form: the FTP leg uses an ftps:// (implicit TLS) listen URL. Program finding: CurlNetworkOptions.UseSsl = UseSsl.All on an ftp:// URL still sent USER and PASS in the clear (no AUTH TLS), so surl refused 530 LoginDenied; the console's --ssl-reqd does send AUTH TLS. Reported for the Curl port.
