<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-log-in-with-cram-md5
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 10 of 10 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The bibliography has no entry for CRAM-MD5 (RFC 2195) or SMTP AUTH (RFC 4954); the Discussion describes the digest only as far as the decode case shows. Suggest the lead adds both keys.
2. The decode script needs a POSIX shell and `base64 -d`; checked on Windows (Git Bash) only. `curl -v` on Windows ends lines CR CR LF, so regexes use `\s+`.
3. Raw PLAIN/LOGIN attempts are sent with `curl -T plain.smtp telnet://...`, the only way the two programs offer to send a command curl itself refuses to send.
