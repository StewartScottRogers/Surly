<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-a-mail-attachment
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. The test is check-attachment.sh (POSIX sh: curl, tr, base64 -d, cmp, wc); base64 -d needs macOS 13 or later, which CI's macos-latest is. Not yet run on Linux or macOS.
2. A missing MIME part is not an error to surl or curl (SECTION=2 of a single-part mail: exit 0, 0 bytes), so the script checks for an empty part first. Case missing-part.
3. MIME (RFC 2045/2046) is not in the bibliography; part numbering and BODYSTRUCTURE are cited to rfc9051. The lead may want an rfc2045 entry.
