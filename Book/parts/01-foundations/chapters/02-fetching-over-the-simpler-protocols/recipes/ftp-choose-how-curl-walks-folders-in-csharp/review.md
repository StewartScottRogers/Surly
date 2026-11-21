<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-choose-how-curl-walks-folders-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 of 13 cases pass on windows.
## Findings
1. Carries the console claims the library can show: the three methods by recorded exchange, the default, the refused CWD (RemoteAccessDenied), missing file and folder, the server log. One CurlClient per fetch for an exchange each.
