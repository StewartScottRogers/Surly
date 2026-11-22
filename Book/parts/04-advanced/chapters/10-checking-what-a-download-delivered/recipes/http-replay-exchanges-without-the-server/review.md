<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-replay-exchanges-without-the-server
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.
## Findings
1. Nearest honest recipe: neither curl nor surl records and replays exchanges, so the recipe records heads and bodies with `-D`/`-o` and replays bodies through `file:` URLs; the Solution says so.
2. `-D` takes no `#1` from a URL glob (it writes a file literally named `#1.head`), hence one curl per path in record.sh. Upstream behaves the same; not a finding against the port.
3. Linux and macOS: replay.sh falls back from `pwd -W` to `pwd`; CI checks.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 404 now carries the body `404 Not Found` (BL-1444), so the recorded 404 replays that line; missing-recorded-as-404 and the Discussion restated.
