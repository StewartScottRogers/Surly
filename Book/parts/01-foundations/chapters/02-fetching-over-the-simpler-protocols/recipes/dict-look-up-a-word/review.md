<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dict-look-up-a-word
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 8 of 8 cases pass on windows.
## Findings
1. Rubric 12: the Solution and several claims use `stdout-matches`, because the greeting carries a connection number (`<2@surl>`) and DICT ends lines with CR LF. verify reads stdout as text (universal newlines on every platform), so the patterns use `\n`. The Solution's printed output is the pattern, which reads badly; the Discussion repeats the reply as a plain listing.
2. Left out on purpose: case-insensitive lookup. On Windows `d:SURLY` finds the file `surly` (and echoes `"SURLY"`), while `m:SURLY:*:exact` finds nothing, and the help says headwords are "compared case and all". Platform-dependent; reported as a program finding, no claim made.
3. Fixed: dropped an unchecked claim that the first `250 ok` answers curl's `CLIENT` line.
4. Sources wanted for the bibliography: RFC 2229 (DICT).
5. The generated Solution block prints `Listening on http://127.0.0.1:8080/` for this non-HTTP listener: build.py hard-codes the scheme. Needs a build fix (lead); not editable from the chapter.
6. Verified after the byte-exact fixture fix; the definitions are compared through patterns, unaffected.
