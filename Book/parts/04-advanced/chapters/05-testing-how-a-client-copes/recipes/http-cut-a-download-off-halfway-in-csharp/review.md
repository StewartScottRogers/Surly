<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-cut-a-download-off-halfway-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. RecvError claimed; on macOS an older Surl left the Curl.Api body read hanging (platforms.md), Timeout 30 s bounds it. Watch CI.
2. Resume is a Range header (Curl.Api has no -C).
- Lead: CI on macOS (batch 13) saw transfers that surl aborts mid-body never end until curl's --max-time; the cut cases are marked [windows, linux], short-answer-passes runs everywhere. Reported to Curl.
