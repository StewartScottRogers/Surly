<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: wss-log-in-over-tls
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 9 of 9 cases pass on windows.
## Findings
1. Truth (4, 7): Basic over `wss` checked (right 0, wrong 401, none 401), over `ws` refused unchecked (403 for right and wrong alike), and under `--allow-plaintext-auth` checked over `ws` (right 0, wrong 401) on a second `surl` on port 8082 are all cases.
2. Fitness (3): Bearer (`--user :tok123`, `--oauth2-bearer`) and the `--digest` refusal go beyond the chapter list's "Basic over wss"; kept because the README's point, that `curl` logs in on an upgrade only with what it sends unasked, needs one case each way. AWS Signature Version 4 is mentioned nowhere here.
3. Fixed: lint found `-u` and `--oauth2-bearer` explained only inside `curl ...` code spans; reworded. The "second surl in the listing" sentence was corrected, as in the FTP recipe.
4. Truth (4): "two surl processes cannot hold one directory" is cased in Part 2 (`http-refuse-a-second-server-on-one-folder`), not here; `--self-signed`/`-k` is a pointer only.
