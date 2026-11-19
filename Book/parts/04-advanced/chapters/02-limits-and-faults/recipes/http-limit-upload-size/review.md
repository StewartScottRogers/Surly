<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-limit-upload-size

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 10 of 10 cases pass on windows (three runs).

## Findings

1. The limit is small (10 bytes) so the fixtures stay readable: `notes.txt` is 17 bytes, `ten.txt` exactly 10, which shows the limit is inclusive.
2. Absence is proven by status: the refused uploads, by length and chunked from standard input, are 404 afterward.
3. The login options (`--allow-uploads`, `--user`, `--digest`) are there because `surl` takes no `PUT` without them; the Discussion says so and points to <<http-accept-uploads>>.
4. `curl --max-filesize` (exit 63) is a claim of its own, since the two programs give the same option opposite directions.
