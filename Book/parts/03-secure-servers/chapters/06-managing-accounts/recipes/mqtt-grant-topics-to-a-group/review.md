<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-grant-topics-to-a-group

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.

## Findings

1. Group syntax is `--group name=member,...` and `@name:right` in `--topic-access`; `surl --help` shows `--group` only under the auth category. Recorded in facts/mqtt.md.
2. A bare group name in a grant is looked up as an account: the refusal says `names no account or group`.
3. Publish then read in one `--next` chain is the book's idiom; the twins wait for the store event.
