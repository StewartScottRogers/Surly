<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-append-and-flag-a-message
Verdict: accept
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 12 of 12 cases pass on windows.
## Findings
1. Title changed from the task's wording to "Appending, Flagging and Expunging Mail": with "Internet Message Access Protocol" spelled out it would be nine words; the key keeps `imap`.
2. The `\Seen` flag on an appended message comes from `curl` (`APPEND INBOX (\Seen)`), not from `surl`; upstream curl 8.22.0 sends the same, so it is stated as curl's behaviour and cased with `-v`.
3. "UIDs are never reused within a mailbox" is cited to <<rfc9051>>; the case shows only that the surviving message keeps UID 2 after an expunge.
