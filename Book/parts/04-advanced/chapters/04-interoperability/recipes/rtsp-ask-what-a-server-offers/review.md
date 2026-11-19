<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: rtsp-ask-what-a-server-offers
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 7 of 7 cases pass on windows.
## Findings
1. Fixed: the second server (with `--user`) first had `--directory site` too and would not start: surl exits 124, `Directory site is in use by another surl process`. It has no directory now (OPTIONS reads no file), and the Discussion says why.
2. Fixed: "on the same connection" is a case, not a citation: `-w "%{http_code} %{num_connects}"` prints `200 1` for the 401-then-200 exchange.
3. Fixed: "OPTIONS * whatever the path" is a case (`-v` trace for `/hello.txt`), not only the README's word.
4. Fitness (3): the solution's expected output is a regular expression, because the answer carries a `Date` header; unavoidable without hiding the headers the recipe is about.
5. Truth (5): that programs choose DESCRIBE, SETUP and PLAY through the library, and that `--allow-plaintext-auth` would accept Basic, are cited (surl-readme, curl-manual), not cases; the libcurl side is `rtsp-play-a-stream-with-libcurl`'s subject.
