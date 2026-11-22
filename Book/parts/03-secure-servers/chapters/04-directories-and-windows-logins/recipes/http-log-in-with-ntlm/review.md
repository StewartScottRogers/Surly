<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-log-in-with-ntlm
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 8 of 8 cases pass on windows.
## Findings
1. Fitness (1): the task is "NTLM and Negotiate", but the Curl port's `--negotiate` cannot log in here: on a Windows machine in no domain it fails `SEC_E_NO_CREDENTIALS` and sends no token (exit 22 with `-f`). That is a case, and the Solution uses `--ntlm`. Upstream curl 8.22.0 behaves the same (commit a8881e5e1d, "spnego: block NTLM fallback in SPNEGO negotiation"; Surl ADR-0040 and ADR-0042 measured it), so it is not a Curl port defect.
2. Truth (5), needs the author: the sentence "From version 8.22.0, curl no longer lets Negotiate fall back to NTLM" has no bibliography key. Add a key for the upstream commit or curl's 8.22.0 changelog, or cut the sentence.
3. Truth (4, 7): the defaults (Digest offered, neither NTLM nor Negotiate; `--ntlm` answered 401) are cases against a strict `surl`; the Negotiate offer is a case against a third with `--auth negotiate`. Absence of NTLM and Negotiate in the default 401 is proved by an anchored regex over the whole response, not a substring.
4. Truth (5): Negotiate with Kerberos (`--keytab`) is cited, not cased; it needs a Key Distribution Center the test does not have.
5. Platform: the `SEC_E_NO_CREDENTIALS` case is Windows (SSPI) only; on Linux and macOS `--negotiate` fails differently or not at all. Verify there before publishing; that case will likely need to move or go.
6. Updated 2026-11-19: CI on Linux and macOS failed `negotiate-sends-nothing` on wording only (GSS-API: `gss_init_sec_context() failed: ...`). It is now `platforms: [windows]` (SSPI's `SEC_E_NO_CREDENTIALS`), and a new portable case, `negotiate-cannot-log-in`, checks exit 22 and no output on every platform. The Discussion names SSPI and GSS-API as the source of the wording. Finding 5 is settled; no Curl port difference from upstream (upstream uses the same libraries).
7. Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 401 now carries the body `401 Unauthorized` (BL-1444); challenge-offers-ntlm and strict-offers-digest match it after the headers.
