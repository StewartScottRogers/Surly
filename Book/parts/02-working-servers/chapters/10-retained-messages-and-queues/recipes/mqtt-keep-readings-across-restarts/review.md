<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-keep-readings-across-restarts

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows (console).

## Findings

1. wait.sh polls (20 tries, half a second apart) for each seed publish before the server is stopped outright by restart:; the retained messages are one file, state/.surl/bus/retained.
2. The lock file stays after a stop; a stale lock is no obstacle to the next start. Missing folder exit 37, folder in use exit 124.
