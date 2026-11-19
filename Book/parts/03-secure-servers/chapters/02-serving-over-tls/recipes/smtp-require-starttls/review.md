<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-require-starttls
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 16 of 16 cases pass on windows.
## Findings
1. Fitness (1): the title says "requiring", and that is `curl --ssl-reqd`: `surl` itself does not require TLS for a `CRAM-MD5` login, which sends no password, and that is cased (`cram-md5-in-clear`) and said plainly, including that the mail itself then crosses in clear. What `surl` withholds without TLS is every mechanism that carries the password or a token.
2. Truth (4): the capability lists before and after the upgrade are cased per protocol with `-v ... 2>&1` and a regex over the whole conversation, because the post-TLS SMTP line starts with the pre-TLS one and `stderr-contains` could not tell them apart.
3. Truth (7): `--allow-plaintext-auth` is cased on a second `surl` (ports 8086-8088): `--ssl-reqd` exits 64 there (SMTP, POP3), and `AUTHENTICATE PLAIN` goes out as `alice`/`s3cret` in base64, which the prose shows as the cost. All nine port placeholders are used.
4. Fixed: "the second surl in the listing" (the build lists only the solution's server) and "imaps or pop3s exit 58" (only `smtps` is cased) were corrected.
5. Not cased: `surl` refusing a clear-password mechanism sent anyway (the README's claim); `curl` will not send an unoffered mechanism, so it is left out. `--self-signed`/`-k` is a pointer only.
