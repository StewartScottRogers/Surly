<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-log-in-with-a-public-key
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.
## Findings
1. Fixed: `-u alice:` (with the colon) in every key login; `-u alice` alone makes curl prompt for a password and hang under a test. The prompt is cited from `curl --manual`, not a case; that manual is not in the bibliography (lead: add upstream curl's manual page if it should be cited by key).
2. Fixed: `--pubkey` kept in the Solution though a case shows it is optional here; the manual says extracting it depends on the build.
3. Not claimed, platform: an Ed25519 client key is refused (exit 67) by this Windows curl (libssh2 on WinCNG); the recipe uses RSA keys throughout.
4. `-k` is used for the host key to keep the recipe on the client's key; the Discussion says so and points to <<scp-pin-the-host-key>>.
