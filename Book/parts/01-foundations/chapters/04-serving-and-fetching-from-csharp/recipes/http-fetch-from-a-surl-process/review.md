<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fetch-from-a-surl-process
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Fixed: dropped "before anything is sent" from the malformed-URL claim; the case shows only the code.
2. Fixed: `--directory` is now named on its own in the Discussion (lint).
3. For the author: case nothing-listening assumes nothing listens on 127.0.0.1 port 1 on the verifying machine. True on the usual Windows, Linux and macOS set-ups, but a machine running tcpmux would break it.
4. The program needs `using Curl.Api.Errors` for `CurlException`; the Discussion says so, since a reader copying only `using Curl.Api` gets a compile error.
5. Fixture site/hello.txt was verified after the byte-exact fixture fix landed; the exact stdout is "hello from surl\n" either way.
6. Same package-feed note as http-serve-and-fetch-in-process.
