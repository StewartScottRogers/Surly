<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-fixture-from-a-config-file
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 8 of 8 cases pass on windows.
## Findings
1. test-env.conf holds the url lines with port 0, so check-env.sh reads the ports from the Listening on lines, as in surl-hand-bound-ports-to-the-app.
2. Program finding, kept as case no-comment-after-a-flag: a `#` comment after a valueless flag (`allow-uploads  # ...`) is refused, exit 2, `text follows the value of --allow-uploads; quote a value that holds a space`. surl --manual says a # after a value starts a comment; a flag has no value, so either the manual or the parser should say what happens. Expected: the comment ignored, as after a value.
3. Loosening options (--allow-plaintext-auth, --allow-uploads) each have a case without them, through options after -K (--no-allow-plaintext-auth: HTTP 403, curl 22; --no-allow-uploads: FTP 550, curl 25).
4. The FTP upload writes site/upload.txt and the mail store writes site/.surl/ in the reader's checkout when run.sh is run; harmless, not claimed.
