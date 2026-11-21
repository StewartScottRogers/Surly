<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-password-without-downtime-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.

## Findings

1. Uses an ftps:// (implicit TLS) listener, not ftp:// with explicit TLS: Curl.Api's `CurlNetworkOptions.UseSsl = UseSsl.All` (request or client) does not send AUTH TLS, the password goes in clear and surl answers 530 `Login needs TLS first` (curl --ssl-reqd works). Reported against Curl.Api.
2. The FTPS job checks the file with HEAD (SIZE only) rather than downloading it: Curl.Api downloads over FTPS failed about 1 in 30 with PartialFile (18), surl logging `The client closed the data connection before the whole file was sent` then 426; 120 console curl downloads (ftp --ssl-reqd and ftps) all passed. Reported; a download would make this recipe flaky.
3. Adds a claim the console cannot show: an FTPS connection logged in as app-v1 keeps working after Remove; only new logins are refused.
