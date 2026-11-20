<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldaps-search-over-tls
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. One surl, an `ldaps://` and an `ldap://` listener on the same directory and account; the accepted bind and the refused one differ only by address.
2. Program finding: on Windows `curl --cacert server.pem ldaps://...` still fails with 60 (SEC_E_UNTRUSTED_ROOT): the WinLDAP path uses the Windows store and ignores `--cacert`. So the solution uses `-k`, and the Discussion says so. Cased as `cacert-windows` and `untrusted`, both `platforms: [windows]`.
3. Not run, so not claimed: what OpenLDAP-based `curl` on Linux and macOS does with `--cacert` and with no `-k` (exit code unknown). `-k` on those platforms is assumed to work like on Windows; CI will show.
4. `ldaps` needs `--cert`; without it surl exits 58 (cased).
