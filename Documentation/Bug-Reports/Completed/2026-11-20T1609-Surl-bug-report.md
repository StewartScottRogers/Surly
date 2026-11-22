<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-20 (batch 5 of the 1,000-recipe plan)

From writing and verifying batch 5 (60 recipes) of the *Surly* cookbook against Surl `9ad60d80c` (factory/phase-1) and Curl `7d167fa10a`, Windows 11. Only findings not already filed (BL-1417..1457) are listed. Thank you for the fixes in this build; the book now shows the redacted logs, the new P12 and surl-mcp messages, and the `-K` comment rule.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | AWS SigV4 | `--auth aws-sigv4` does not check the credential scope: a request signed for another region or service is accepted |
| 2 | Medium | Mail (widens BL-1450) | SMTP mail to an account added at run time (`add_account` / `Accounts.AddAsync`) is answered 250 and then discarded; `Mail.DeliverAsync` to the same account throws `NoSuchAccount`. A refusal at `RCPT TO` would match |
| 3 | Low | SFTP log | A refused public key is logged as refused password and keyboard-interactive attempts; no line names the publickey attempt or its key fingerprint |
| 4 | Check | MQTT retain | A retained message seeded through `PublishAsync` or `publish_message` (retain true) is kept under a topic outside `--topic`'s filter, while the same publish from curl is not retained. One of the two looks unintended |
