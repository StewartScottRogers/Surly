<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-reproduce-a-bug-report-as-a-test
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 6 of 6 cases pass on windows.
## Findings
1. The reported bug is curl's documented -d behaviour (strips CR and LF from @file); the recorded body proves the client, not the server, dropped them. Cited to curl-manual.
2. PUT into a folder that does not exist is 409 on surl; the stand-in seeds reports/ through files.
3. Exit codes: 0 pass, 1 reproduced, 2 stand-in showed neither (405 without --allow-uploads, the loosening option's refusal case).
4. JsonSerializer.Serialize throws in file-based apps (reflection off); the program quotes strings by hand.
