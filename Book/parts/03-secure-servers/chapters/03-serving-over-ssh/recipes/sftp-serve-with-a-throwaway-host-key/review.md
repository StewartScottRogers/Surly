<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-serve-with-a-throwaway-host-key
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.
## Findings
1. Fixed: the refusal without `-k` uses `--knownhosts known_hosts` (an empty fixture), so it is exit 60 on every machine. Without `--knownhosts`, curl exits 2 (`Could not find a known_hosts file`) only where the user has no `~/.ssh/known_hosts`; that depends on the reader's machine and is not claimed.
2. Fixed: the loosening has both sides as cases: no host key refused at start (exit 2), throwaway key served; and `-k` is shown to skip only the host-key check (wrong or missing password still exit 67).
3. Truth (5): the `surl: warning:` line on start, naming the hash, is the Surl README's claim, cited; verify cannot read the server's log.
4. Fixed: SSH, SFTP, SCP and RSA spelled out on first use.
