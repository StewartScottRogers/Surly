<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-one-folder-every-way-from-an-agent
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. transfer statusCode differs by scheme: http 200, ftp 226, tftp/gopher/dict 0, ws 101; failure is the field to judge by.
2. Without --allow-anonymous the FTP transfer failure text is 'Access denied: 530' then 'curl exit code 67 (LoginDenied)'.
