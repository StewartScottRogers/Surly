<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-fail-a-test-on-mail-to-another-domain
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 9 of 9 cases pass on windows.
## Findings
1. Distinct from smtp-fail-a-test-on-a-wrong-recipient (allowfails, --next, no domain, anonymous): this one pins what counts as foreign with two domains: subdomain and look-alike refused, letter case ignored, sender and header fields not judged.
2. The log is read through wait-for-log.sh, a bounded poll (10 s) for a count of lines; exchange numbers are stripped because verify's readiness probe shifts them.
