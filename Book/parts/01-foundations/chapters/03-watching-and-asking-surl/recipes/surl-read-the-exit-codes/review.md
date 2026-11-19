<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-read-the-exit-codes

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 9 of 9 cases pass on windows.

## Findings

1. Fixed: "-s silences that line" is now scoped to the 124 line, the one checked. `-s` does not silence the exit-1 line (program finding in the chapter report).
2. Fixed: added a case for `--version` listing the schemes, since the Discussion points to it; dropped "--self-signed warns on every start", which had no case here.
3. Needs the author: the generated Solution sentence reads "`surl` prints that it is listening, then `surl` prints what it got", and the generated run.sh has `set -e`, so the second surl's exit 124 ends the script before `kill %1` and leaves the first surl running. Both are build.py's to fix for a recipe whose solution is meant to fail.
4. Cases that start a surl expected to refuse use port 0 with `timeout: 10`, so a regression that let it serve fails the case instead of hanging verify.
5. <<https-serve-with-a-self-signed-certificate>> points at a recipe not yet written.
6. Updated 2026-11-19 for Surl commit 993aa7072: Surl fixed the defect in finding 1, so `-s` now silences the exit-1 line too (exit 1 counts as a failure found once the command line is read). Added case `silent-unsupported-scheme` and said so in the Discussion. The 124 row of `--aihelp exit-codes` now also names a writable `--share` folder and marking the share `;readonly`; case `look-it-up` pins the facts its claim states (name, meaning, stopping the other surl, another `--directory`, serving in memory without `--directory`) rather than the whole row.
