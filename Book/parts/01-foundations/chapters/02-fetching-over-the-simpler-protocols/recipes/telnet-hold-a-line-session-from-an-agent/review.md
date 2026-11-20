<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: telnet-hold-a-line-session-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.

## Findings

1. The curl tool's `stdin` feeds the session; a no-quit session with `-m 2` is exit 28, a tool error of kind CurlExit.
2. Whether a chunk of get_exchanges is base64 or text varied between runs (negotiation bytes and lines can share a chunk), so the program decodes each chunk by its encoding and searches the whole.
