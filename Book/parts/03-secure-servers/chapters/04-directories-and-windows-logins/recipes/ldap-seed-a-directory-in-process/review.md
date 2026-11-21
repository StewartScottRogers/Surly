<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-seed-a-directory-in-process

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.

## Findings

1. A batch is all or none (SurlSeedRefusedException); ExportLdifAsync omits userPassword; rfc2849 is not in the bibliography, so it is not cited.
