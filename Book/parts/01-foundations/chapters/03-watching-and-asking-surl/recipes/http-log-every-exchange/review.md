<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-log-every-exchange

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 12 of 12 cases pass on windows.

## Findings

1. Fixed: the Discussion first said each level "writes what the one below it writes and more", described headers and body in the verbose log, and said `-s` "silences everything". None had a case; cut to what the cases show (request line, the `*` line, status line; `-s` silences the failure line checked).
2. Fixed: lint wanted `-S`, `-o` and `-w` named on their own in the Discussion.
3. Needs the author: the log file sits inside the served folder (`--log-file site/surl.log`) only so verify can fetch it with `curl`; the Discussion says so and warns against it. A schema for server-log assertions (AI.md, "The schema") would remove the trick.
4. Not claimed, for want of an observable case: the default (info) level's one line per exchange, the Listening on line going quiet under `-s`, and `--log-file -`. verify cannot see a running server's own output.
5. Not claimed: `-s` silencing an exit-1 failure. It does not (see the program finding in the chapter report); the recipe uses exit 58 instead.
6. The 16-byte size in the log line was checked after verify.py began writing fixtures byte-exact (LF only); no case was tuned to CRLF bytes.
