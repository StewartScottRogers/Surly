<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-trust-a-test-ca-end-to-end
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 10 of 10 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Two surls because surl serves one --cert per process; the mail one serves SMTPS and IMAPS. A third (stray) carries a certificate another CA signed: --cacert ca.pem refuses it 60, -k accepts it (the loosening shown both ways).
3. Windows Schannel needs --ssl-no-revoke even for a leaf signed directly by the root (60 'revocation status is unknown' without); that case is [windows]; Linux and macOS not probed, CI checks the rest.
