<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-accept-basic-only-over-https
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. One surl with an `https://` and an `http://` listener (ports 1 and 2) and `--auth basic`; a second server without `--auth` is there only for the default-offers-Digest case.
2. Program finding, not a defect: with `--auth basic`, a request with no login over `http://` is answered 403 with no `WWW-Authenticate`, not 401. Claimed and cased as it is.
3. The start-up warning of `--auth` is cited to the README: verify cannot see surl's log.
4. `--http1.1` is used where headers are matched, since HTTP/2 lower-cases header names.
