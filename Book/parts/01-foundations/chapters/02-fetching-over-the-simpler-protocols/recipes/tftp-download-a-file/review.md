<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-download-a-file
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 5 of 5 cases pass on windows.
## Findings
1. The server is `ready: none`: TFTP is UDP and verify.py can wait only for a TCP port, so it sleeps 0.2 s. Passed five runs in a row on Windows; may race on a slow machine. A UDP or `stderr-contains` readiness in verify.py (the template documents `stderr-contains`, verify does not implement it) would remove the risk. Needs the lead.
2. Rubric 7: why `--allow-uploads` is off is cited to <<surl-readme>> (the security help says every widening option is off by default); the TFTP-specific reason is reasoning from "no login", not a case.
3. The `files:` expectation of `save-to-file` and the `write-refused` case show only "exit 0"/"exit 69" in the claims table; the build does not print `files:` results.
4. Sources wanted for the bibliography: RFC 1350 (TFTP), RFC 2347-2349 (options).
5. The generated Solution block prints `Listening on http://127.0.0.1:8080/` for this non-HTTP listener: build.py hard-codes the scheme. Needs a build fix (lead); not editable from the chapter.
6. Verified after the byte-exact fixture fix; no claim depends on line-ending bytes.
