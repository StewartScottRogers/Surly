<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-serve-a-mail-domain

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 9 of 9 cases pass on windows.

## Findings

1. Fixed: logins over plain SMTP and IMAP need no loosening option: surl offers only CRAM-MD5 without TLS and curl picks it. Added a case (`cram-md5-only`) for that claim, and pointed at `--allow-plaintext-auth` by citation only.
2. Fixed: dropped "the message is discarded" for an unknown in-domain recipient; the case proves only the `250`.
3. The generated Solution text says "`curl` prints what it got", but this solution prints nothing (the send succeeds silently); build.py's to word for a silent solution.
4. curl uses the URL path as the EHLO name, so with `-T mail.txt` and a URL ending in `/` the stored message carries `Received: from mail.txt`. Upstream curl behaviour; not mentioned in the prose.
5. <<smtp-require-starttls>> points at a recipe not yet written.
