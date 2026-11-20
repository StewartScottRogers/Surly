<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-call-with-a-proto-file
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 10 of 10 cases pass on windows.
## Findings
1. Plan task changed, nearest honest recipe: 'a production server has no reflection' cannot be built, because surl always serves reflection (`surl --aihelp grpc`; no option turns it off). The recipe shows instead that `--grpc-proto` is what curl reads: `renamed.proto` (same field numbers, other names) changes the JSON names in and out; `file-names-only` and `reflection-names-only` are the two refusals; a method missing from the file is exit 2; a file that does not parse is exit 26.
2. `wrong-service`: curl trusts the file, so a service the server does not serve is `UNIMPLEMENTED` (12), exit 22, with no reflection protection.
3. Fixtures: echo.proto is the file `surl --aihelp grpc` prints; the others are written here.
