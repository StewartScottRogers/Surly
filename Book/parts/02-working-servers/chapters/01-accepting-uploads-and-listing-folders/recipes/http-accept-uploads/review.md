<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-accept-uploads
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 15 of 15 cases pass on windows.
## Findings
1. Fixed: each refusal and its loosening is a case on its own server: no account (401 even with `--allow-uploads`), `--allow-anonymous` (201 with no login), uploads off (405), Basic in clear (403), wrong password (401).
2. Fixed: a two-URL `-o` case was split; `-o` covers only the first URL, so the second body would have reached stdout.
3. Needs the lead (build): the generated Solution block starts all four servers, though the solution uses only `main`; a reader copying it starts three servers they do not need. Likewise run.sh kills only `%1`, leaving the others running, and the Listening block omits the `surl: warning:` line `--allow-anonymous` writes. A way to mark the solution's server would fix all three.
4. Truth (4): "loses them when it stops" and "warns on every start" are cited to <<surl-readme>>, not cased; verify cannot restart a server or read its log.
5. <<https-serve-with-a-self-signed-certificate>>, <<http-require-a-login>> point at recipes not yet written.
