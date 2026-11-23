<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-cut-a-download-off-halfway-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. The 5 MB file is written to disk and served with --directory: start_server files would exceed the 4 MiB message limit.
2. transfer has no resume-to-file field: the curl tool with -C - is used.
- Lead: CI on macOS (batch 13) saw transfers that surl aborts mid-body never end until curl's --max-time; the cut cases are marked [windows, linux], short-answer-passes runs everywhere. Reported to Curl.
