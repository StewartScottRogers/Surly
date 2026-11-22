<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-check-the-flags-an-app-left
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 8 of 8 cases pass on windows.
## Findings
1. The app's run is in seed: (reads, a header read, a STORE); the check is FETCH 1:* FLAGS, which sets no flag; shown with SEARCH UNSEEN afterwards.
2. Checking by fetching the message sets \Seen (curl uses BODY[]); a case shows it and the STORE -FLAGS that undoes it.
3. Chapter 11's intro still speaks of FTP only; it now also holds this IMAP triplet (chapter.adoc left to the lead).
