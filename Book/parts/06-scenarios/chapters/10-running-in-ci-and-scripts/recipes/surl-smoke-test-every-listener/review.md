<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-smoke-test-every-listener
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.
## Findings
1. Nearest honest check without a loosening option: 'answers' means curl got any reply or exited 0. FTP cannot be asked anything before a login, and plain-text FTP passwords are refused unchecked, so FTP passes on its 530 (exit 67).
2. curl gives no %{response_code} for IMAP (000 with exit 0), so IMAP passes on exit 0.
3. --fault on a second server shows a listener that accepts but never answers (exit 28 after -m 3); the main server runs without it.
