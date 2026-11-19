<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-accept-a-bearer-token

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 8 of 8 cases pass on windows.

## Findings

1. Decision: the Solution is over HTTPS, with `setup:` making the certificate, because a Bearer token over `http://` is refused 403 unchecked by default. Two more servers show the refusal (`plain`) and the loosening (`loose`, `--allow-plaintext-auth`), each with a right and a wrong token, so "unchecked" and "checked" are both cases.
2. Title changed to "Accepting a Bearer Token over HTTPS" to say what the Solution does; the key is unchanged.
3. Program note: a second token (`--user :a --user :b`) was refused as "user  is given twice", with two spaces where the empty name is. Reported (2026-11-18T2025 Surl report #1) and fixed in Surl 71807f05f: the message now says "a Bearer token (empty user name) is given twice". Updated 2026-11-19 to match. One server holds one token.
4. The other servers' commands are given in the prose, since the generated Solution prints only `main`.
