<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-trace-every-byte

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 8 of 8 cases pass on windows.

## Findings

1. Fixed: removed unchecked claims (trace is "the loudest level", same shape as upstream `curl --trace-ascii`, the comparison with `--trace`, passwords in the trace, "local" time).
2. Needs the author: the chapter list names `--trace-ascii -` (trace to standard output). verify cannot see the server's standard output, so the recipe traces to `site/trace.txt`, inside the served folder only so verify can fetch it; `-` is left out of the Discussion.
3. Needs the author: the claims table shows regexes containing `<=` and `=>`, which Asciidoctor's replacements may render as arrows inside monospace. Worth a look at the rendered page; the fix belongs in build.py (passthrough for regexes), not here.
4. Note: the trace's `*` line names the served file with backslashes written as `\x5C` on Windows. The regexes do not depend on it.
5. Fixture sizes (16 bytes, 0x10) were checked after verify.py began writing fixtures byte-exact; nothing was tuned to CRLF bytes.
