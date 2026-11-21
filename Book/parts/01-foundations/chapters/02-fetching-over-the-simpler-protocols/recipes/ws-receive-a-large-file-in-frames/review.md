<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-receive-a-large-file-in-frames

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.

## Findings

1. Decision: the frame count comes from surl's own log (`Sent N bytes of /f as a binary message in K frames`), read with `grep` from a `--log-file` beside the served folder; the Solution needs a POSIX shell for `cmp` and `&&`.
2. surl cuts a file into frames of 65,536 bytes: 65,536 is 1 frame, 65,537 is 2, 200,000 is 4, 131,072 is 2, 131,073 is 3. `curl -w %{size_download}` counts frame bytes and is not claimed.
