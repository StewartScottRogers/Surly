<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-resume-only-if-unchanged

Verdict: accept

Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0 / Curl.Api 0.2.0 from the p7 feed; windows only.

## Findings

1. curl has no If-Range option: the ETag is saved with --etag-save and sent with -H; a script chooses append (206) or replace (200), since -C - throws away a 200 body (exit 33).
2. The interruption is stood in for by -r 0-99; console curl takes ~4 s per process here, so a same-second rewrite cannot be shown on the console (see the C# twin).
