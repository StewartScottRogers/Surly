<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smb-read-a-share
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 12 of 12 cases pass on windows.
## Findings
1. Platform: the Curl port build lists `smb smbs` in `curl -V` (a case) and reads the share; nothing had to be left out. The Surl README notes the Git for Windows curl does not list `smb`; the prose tells the reader to check `-V`.
2. Truth (4, 7): the default refusal (exit 67) is a case against a strict `surl`, and "`--auth ntlm` never accepts NTLMv1" is a case against a third `surl` with `--auth ntlm`.
3. Truth (5): uploads needing `--allow-uploads` and `smbs` keeping the response off the wire are cited to <<surl-readme>>; only the refusal of `-T` (exit 9) is cased. `--allow-uploads` is not in this recipe's options, so no loosening case is owed here.
4. The server's own reason for a refused session setup (`ntlmv1 is not in --auth`) is logged by `surl` only; the client sees exit 67 either way, so the prose does not claim the message.
5. Exit 3 for a URL with no share is `curl`'s own check (`missing share in URL path for SMB`), stated as such.
