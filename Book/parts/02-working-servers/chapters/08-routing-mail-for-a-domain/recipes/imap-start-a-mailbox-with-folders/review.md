<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-start-a-mailbox-with-folders
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. surl lists INBOX first, then by name, and offers no SPECIAL-USE attributes (\Sent, \Drafts; CAPABILITY has no SPECIAL-USE, and `LIST ... RETURN (SPECIAL-USE)` is refused), so a client that finds folders by attribute will not find these. Not claimed beyond the LIST output; worth a Surl feature note.
2. The duplicate-folder refusal reads `member Sent is given twice`; "member" is odd wording for a folder (program finding, minor).
3. `folders=INBOX` is accepted silently with no second INBOX; not claimed.
4. A nested `Archive/2026` brings an implied `Archive` marked `\Noselect \HasChildren`.
