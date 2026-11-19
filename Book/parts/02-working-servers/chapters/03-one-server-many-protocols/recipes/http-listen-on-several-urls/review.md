<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-several-urls
Verdict: accept
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 5 of 5 cases pass on windows.
## Findings
1. None outstanding. Every Discussion claim is a case, apart from the `Listening on` lines, which the generated Solution prints and <<surl-readme>> documents.
2. Note: `localhost` as the third listen URL bound IPv6 `[::1]` on Windows (surl's log shows the exchange from `[::1]`); curl reached it either way. On a machine without IPv6 loopback this case may need `127.0.0.1`; worth watching on the Linux and macOS runs.
