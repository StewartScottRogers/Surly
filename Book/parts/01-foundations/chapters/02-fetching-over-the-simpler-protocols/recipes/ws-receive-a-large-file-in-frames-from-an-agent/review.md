<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-receive-a-large-file-in-frames-from-an-agent

Verdict: accept with fixes

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.

## Findings

1. Decision: the frame count cannot be read from `get_exchanges` alone. The record shows only the first 64 KiB (`shown_bytes` 65536, `is_cut` true) while `recorded_bytes` counts all, so the first frame's header (`0x02 0x7f`, final bit clear, length 65536) is the evidence the message continues; the framing bytes from `transfer`'s `info.sizeDownload` (36, 12, 14) give the count.
2. The console recipe's four-frames claim (a log line) has no agent twin: `start_server` `log_lines` holds only the start-up lines and `read_log` is for Kafka topics.
