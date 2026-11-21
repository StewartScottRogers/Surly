<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-multiplex-requests-over-http2
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.
## Findings
1. Removed, not deterministic: `--parallel-immediate` gave 1,1,1,1,1 by hand and 1,0,0,0,0 under verify; the Discussion cites curl's docs for the wait-to-multiplex default instead.
2. Parallel is shown by wall time (`elapsed.sh`, `date +%s`, POSIX shell) against a second surl with `--fault delay` 2 s: 2-4 s with `-Z`, 10 s or more without; the Discussion says the fault is there only for the clock.
3. Server-side proof: the `--trace-ascii` GET lines under one exchange number (a backreference in the pattern), on files no other case fetches; `&&` works in cmd.exe and sh.
4. `-Z` prints in finish order, so the Solution prints only `2` per transfer; the one-connection claim is its own case.
