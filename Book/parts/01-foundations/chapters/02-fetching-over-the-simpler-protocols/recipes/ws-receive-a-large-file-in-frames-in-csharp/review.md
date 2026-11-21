<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-receive-a-large-file-in-frames-in-csharp

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.

## Findings

1. Curl.Api hides frames: one `ReceiveAsync` gives the whole `Binary` message and the next is `Close`. The frame count is read from the server's `Log` at `LogLevel.Verbose`, the same line as the console recipe; the console's `grep` has no C# counterpart beyond that.
2. The program waits up to 5 s for the last log line, since the line is written as the exchange finishes.
