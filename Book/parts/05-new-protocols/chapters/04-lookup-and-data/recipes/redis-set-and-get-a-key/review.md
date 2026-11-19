<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-set-and-get-a-key
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 13 of 13 cases pass on windows.
## Findings
1. Fitness (3): the Solution uses `--allow-anonymous`, as `surl --aihelp redis`'s own "for a test" example does and as mqtt-publish-and-subscribe does; the account, `--allow-plaintext-auth` and `rediss://` with a certificate from `setup:` are cases on three more servers, so each refusal and its loosening is checked.
2. Fixed: a sentence about asking `EXISTS` to tell an empty value from a missing key had no case; removed.
3. Fixed: the Solution's printed block includes the `openssl` setup line, which only the `tls` server needs; the sentence under the block says so.
4. Truth (5): over `redis://` without `--allow-plaintext-auth`, surl answers the right password with `WRONGPASS` (as MQTT answers CONNACK 5), so a reader cannot tell a clear-text refusal from a wrong password by the reply. The Discussion says so; the author may want surl to answer a distinct error.
5. Cited, not cased: `HELLO 3` (RESP3) and the `redis-cli --raw` output form (<<curl-protocols>>, Curl ADR-1079), the `surl: warning:` lines on start and the keys being memory-only (<<surl-readme>>).
6. Exit 60 on the untrusted certificate is checked by exit code only; the message is Schannel's on Windows and differs elsewhere.
