<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-cut-a-download-off-halfway

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. Nearest honest form: surl's drop;after-bytes aborts (RST), so curl exits 56, not 18 (partial file) as the task expected. Reported.
2. The reset discards unread bytes at the client, so the partial size varies (204,603 of 500,000 seen under load at a smaller scale); the recipe uses a 5,000,000-digit file cut at 4,000,000 and claims 1,000,000-3,999,999, which keeps one -C - resume always enough.
3. No log claim: the drop line is logged only at -v, which writes the whole body to the log.
4. Linux/macOS not run: exit 56 is expected there (ECONNRESET); platforms.md notes a macOS Curl.Api body read that never ended after an AfterBytes drop at an older Surl, so CI should be watched.
- Lead: CI on macOS (batch 13) saw transfers that surl aborts mid-body never end until curl's --max-time; the cut cases are marked [windows, linux], short-answer-passes runs everywhere. Reported to Curl.
