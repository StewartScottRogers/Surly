<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: scp-serve-a-host-certificate
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), OpenSSH 10.5p1 (Git for Windows): 9 of 9 cases pass on windows.
## Findings
1. Needs the author: the Curl port cannot verify a host certificate. Its libssh2 1.11.1 lists the `-cert-v01` host-key algorithms after the plain ones, so it always receives the plain key (seen in surl's log), and it ignores a `@cert-authority` line in `--knownhosts` (exit 60). So the client that trusts the CA is OpenSSH's `sftp`, in the solution; what `curl` does (plain-key pin works, CA line does not) is two claims. Same question as in sftp-offer-weak-algorithms-for-old-clients about a non-curl solution client.
2. Needs the author (build defect): `run.ps1` gets `/dev/null` in setup lines, not `NUL`: build.py substitutes setup with POSIX values before its `{{null}}` to `NUL` replacement runs, so the two `curl ... -o {{null}} file:///{{null}}` setup lines would fail in run.ps1. verify (cmd, `NUL`) and run.sh are right.
3. The `known_hosts` line is written by `curl --expand-write-out` so it is one command in sh, cmd and PowerShell; it starts with a space because a `-w` value starting with `@` names a file. The case `known-hosts-line` shows OpenSSH reads it as a CA line. On POSIX the URL is `file:////dev/null`; not run here (Windows only).
4. Fixed: refusals at start are cases: missing file 37, plain public key 2, certificate for a key surl does not serve 2. A user certificate (also refused, per --aihelp) is left out: signing the client's key would make `sftp` offer it at login.
5. Truth (5): that surl offers the key under `-cert-v01` algorithms and does not judge validity is cited (surl-readme); the case `certificate-checked` shows a client receiving and accepting the certificate. `-n 127.0.0.1` is explained as what it does, not tested (no case for a wrong principal).
6. Reader: as in the weak-algorithms recipe, use one OpenSSH's `ssh-keygen` and `sftp`; Windows' OpenSSH refuses a key file others can read.
