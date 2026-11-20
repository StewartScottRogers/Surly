<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-snapshot-before-a-risky-step

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.

## Findings

1. Another server's snapshot is now refused with its own message, naming the server that keeps it (fixed in the re-pin; was the same as a dropped one).
2. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: restore_server's two refusals reworded; the cases and the Discussion quote the new texts.
