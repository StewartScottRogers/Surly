<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: curl-parameterise-a-suite-with-variables
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
5 of 5 cases pass on Windows.
1. Cases set the environment with `env NAME=value curl ...`, which runs under cmd.exe (Git's env.exe) and a POSIX shell alike; first recipe to do so.
2. The token goes over plain HTTP, so the servers loosen with `--allow-plaintext-auth`; the refusal without it (403) is a case.
