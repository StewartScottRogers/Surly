<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-offer-weak-algorithms-for-old-clients
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), OpenSSH 10.5p1 (Git for Windows): 8 of 8 cases pass on windows.
## Findings
1. Needs the author: the Curl port has no option that narrows the SSH algorithms it offers, so the old client is OpenSSH's `sftp -o Ciphers=aes128-cbc`, and the solution's client is `sftp`, not `curl`. `curl` appears in the claims, against a surl narrowed with `--ssh-ciphers aes128-cbc --ssh-macs hmac-sha1` and a 1,024-bit RSA host key, which it accepts. If the book must keep `curl` as every solution's client, the narrowed-server case could be the solution instead, at the cost of the problem being a server, not an old client.
2. Fixed: the loosening has both sides as cases: the old client refused by default (`no matching cipher found`, exit 255) and served with the option; weak cipher, weak MAC and 1,024-bit key each refused at start without it (exit 2).
3. Fixed: the start-up warning is a case: a second surl with the option on a taken port writes the warning, then exits 45. verify cannot read a running server's log, so this is the only way to see it.
4. Fixed: three servers need three data directories or none (surl locks a directory, exit 124): the default server has none, the others `site` and `old`.
5. Truth (5): which algorithms the option adds, and why they are weak, is cited from the Surl README and RFC 8332, not a case each. The order of the offer (strong first) was seen in surl's log and left out of the prose.
6. Reader: `sftp`'s private key must not be readable by others; Windows' own OpenSSH 9.5 refused a key made by Git's `ssh-keygen` in the test folder ("bad permissions"). Use the `ssh-keygen` and `sftp` of one OpenSSH; the two from Windows worked together.
