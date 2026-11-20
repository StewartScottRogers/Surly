<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-map-a-bucket-to-a-folder-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 10 of 10 cases pass on windows.

## Findings

1. All console claims carried, plus `read_file`, `list_files` of the root and the start refusal as a tool error with `exit_code` 37.
