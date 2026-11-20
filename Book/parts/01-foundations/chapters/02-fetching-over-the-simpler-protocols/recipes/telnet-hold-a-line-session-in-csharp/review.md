<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: telnet-hold-a-line-session-in-csharp

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.

## Findings

1. Program finding: Curl.Api 0.2.0 sends no line to a TELNET server. A CurlRequest Body (bytes or stream, GET, PUT or POST), a stream upload, or standard input is not sent; the session opens, the banner arrives, and the body never ends. So the line session runs over a TcpClient and Curl.Api is used for the banner and `CurlTelnetOptions` (TTYPE=vt100, which the server's log records). On the console and from an agent, curl sends stdin.
2. The recipe's prose says this as the reason for the socket; there is no case for the negative.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: Curl.Api now sends a request body to a TELNET server (BL-5594), so the whole session runs through CurlClient (PUT with the lines as Body; the body holds no option bytes) and the TcpClient is gone; finding 1 no longer holds. 5 of 5 pass, outputs unchanged.
