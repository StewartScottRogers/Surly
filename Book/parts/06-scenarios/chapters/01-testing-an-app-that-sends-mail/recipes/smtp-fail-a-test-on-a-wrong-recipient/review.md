<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-fail-a-test-on-a-wrong-recipient
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 10 of 10 cases pass on windows.
## Findings
1. Two curl behaviours that hide a stray address are cased: --mail-rcpt-allowfails, and --next (curl's exit code is the last transfer's); --fail-early is the fix. Both are upstream curl behaviour, cited to <<curl-readme>>.
2. Servers open (no --mail-domain) and anon (--allow-anonymous) exist only for the claims; the anonymous case shows the loosening option does not give a domain check.
3. Dropped a draft clause that 'any SMTP library' sees the same 550; no case shows it.
