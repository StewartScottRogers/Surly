<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-read-the-exit-codes

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 8 of 8 cases pass on windows.

## Findings

1. Fixed: "-s silences that line" is now scoped to the 124 line, the one checked. `-s` does not silence the exit-1 line (program finding in the chapter report).
2. Fixed: added a case for `--version` listing the schemes, since the Discussion points to it; dropped "--self-signed warns on every start", which had no case here.
3. Needs the author: the generated Solution sentence reads "`surl` prints that it is listening, then `surl` prints what it got", and the generated run.sh has `set -e`, so the second surl's exit 124 ends the script before `kill %1` and leaves the first surl running. Both are build.py's to fix for a recipe whose solution is meant to fail.
4. Cases that start a surl expected to refuse use port 0 with `timeout: 10`, so a regression that let it serve fails the case instead of hanging verify.
5. <<https-serve-with-a-self-signed-certificate>> points at a recipe not yet written.
