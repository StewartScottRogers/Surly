<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-seed-mail-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Fixed: removed three uncased clauses (`message_base64`, "the refusal an SMTP client would get", "as a fetch does") and "relays nothing for a stranger".
2. Two listeners on one store (SMTP and IMAP) so the key's SMTP is earned: the SMTP case shows a client refused at MAIL (530, curl exit 55) until it logs in, while `seed_mail` needs no login.
3. "read_message sets no flag" is proven positively: after it reads UID 2, `list_mailboxes` still counts one unseen (UID 1 was seen by the IMAP fetch).
4. Not claimed (observed): `seed_mail` matches the account by the local part only, case-insensitively; `ALICE@example.com` and `alice@other.org` both deliver to alice. Left out as an oddity for the author to judge, not a book claim.
5. `uid_validity` and `internal_date` vary per run and are not matched.
