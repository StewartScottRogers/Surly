<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-tunnel-through-a-proxy

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.

## Findings

1. The proxy's CONNECT and tunnel lines are read from --log-file with -v (no trace file, so exchange lines go to the log file); grep -c shows the GET never appears in clear.
2. Certificate failure matched on curl: (60) only (wording is the TLS library's). Refusal uses a --fault rule (503), exit 7.
