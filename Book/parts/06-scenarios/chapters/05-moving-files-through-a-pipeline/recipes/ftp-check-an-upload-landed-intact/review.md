<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-check-an-upload-landed-intact
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. Failure paths: uploads off (25), file too big (70, --max-filesize), login refused (67), and a damaged stored copy (put_file flips one byte) to show the check can say False. Only --allow-anonymous warns on start; --allow-uploads does not.
2. read_file answers base64, so the comparison is byte exact; TYPE I before STOR is in get_exchanges.
